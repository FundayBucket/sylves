﻿using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY
using UnityEngine;
#endif

using static Sylves.VectorUtils;

namespace Sylves
{
    /// <summary>
    /// Class contains utiliites for analysing a MeshData, specifically
    /// for use with MeshGrid.
    /// </summary>
    internal static class MeshGridBuilder
    {
        #region 2d
        public static DataDrivenData Build(MeshData meshData, MeshGridOptions meshGridOptions)
        {
            return Build(meshData, meshGridOptions, out var _);
        }

        public static DataDrivenData Build(MeshData meshData, MeshGridOptions meshGridOptions, out EdgeStore edgeStore)
        {
            var data = new DataDrivenData
            {
                Cells = new Dictionary<Cell, DataDrivenCellData>(),
                Moves = new Dictionary<(Cell, CellDir), (Cell, CellDir, Connection)>(),
            };
            edgeStore = BuildMoves(meshData, meshGridOptions, data.Moves);
            BuildCellData(meshData, meshGridOptions, data.Cells);
            return data;
        }

        private static void BuildCellData(MeshData data, MeshGridOptions meshGridOptions, IDictionary<Cell, DataDrivenCellData> cellData)
        {
            for (var submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                var face = 0;
                foreach (var faceIndices in MeshUtils.GetFaces(data, submesh, meshGridOptions.InvertWinding))
                {
                    var cell = new Cell(face, submesh);
                    Deformation deformation;
                    if(data.GetTopology(submesh) == MeshTopology.NGon)
                    {
                        // Currently ngon doesn't support deformation, so do something else
                        var centroid = faceIndices.Select(x => data.vertices[x]).Aggregate((x, y) => x + y) / faceIndices.Count;
                        deformation = Deformation.Identity * Matrix4x4.Translate(centroid);
                    }
                    else
                    {
                        deformation = DeformationUtils.GetDeformation(data, face, submesh, meshGridOptions.InvertWinding);
                    }
                    var cellType = GetCellType(faceIndices.Count, meshGridOptions.DoubleOddFaces);
                    var trs = GetTRS2d(deformation, Vector3.zero);

                    if (meshGridOptions.UseXZPlane)
                    {
                        cellType = XZCellModifier.Get(cellType);
                        deformation = deformation * RotateYZ;
                        trs = new TRS(trs.ToMatrix() * RotateYZ);
                    }

                    cellData[cell] = new MeshCellData
                    {
                        CellType = cellType,
                        Deformation = deformation,
                        TRS = trs,
                        Face = faceIndices,
                    };
                    face++;
                }
            }
        }

        private static TRS GetTRS2d(Deformation deformation, Vector3 p)
        {
            var center = deformation.DeformPoint(p);
            var e = 1e-4f;
            var x = (deformation.DeformPoint(p + Vector3.right * e) - center) / e;
            /*
            var z = (deformation.DeformPoint(p + Vector3.forward * e) - center) / e;
            var y = Vector3.Cross(x, z).normalized;
            */
            var y = (deformation.DeformPoint(p + Vector3.up * e) - center) / e;
            var z = Vector3.Cross(x, y).normalized;
            var m = ToMatrix(x, y, z, new Vector4(center.x, center.y, center.z, 1));

            return new TRS(m);
        }

        // Loop over every edge of every face, match them up pairwise, and marshal into moves array
        // This relies on the fact that for 2d cell types, the number of the edge corresponds to the CellDir.
        // Returns any unmatched edges
        private static EdgeStore BuildMoves(MeshData data, MeshGridOptions meshGridOptions, IDictionary<(Cell, CellDir), (Cell, CellDir, Connection)> moves)
        {
            var vertices = data.vertices;
            var edgeStore = new EdgeStore();

            for (var submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                var face = 0;
                foreach (var faceIndices in MeshUtils.GetFaces(data, submesh, meshGridOptions.InvertWinding))
                {
                    int first = -1;
                    int prev = -1;
                    int indexCount = 0;
                    foreach (var index in faceIndices)
                    {
                        if (first == -1)
                        {
                            first = index;
                            prev = index;
                        }
                        else
                        {
                            var cellDir = EdgeIndexToDir(indexCount, faceIndices.Count, meshGridOptions.DoubleOddFaces);
                            edgeStore.AddEdge(vertices[prev], vertices[index], new Cell(face, submesh), cellDir, moves);
                            prev = index;
                        }
                        indexCount++;
                    }
                    {
                        var cellDir = EdgeIndexToDir(indexCount, faceIndices.Count, meshGridOptions.DoubleOddFaces);
                        edgeStore.AddEdge(vertices[prev], vertices[first], new Cell(face, submesh), cellDir, moves);
                    }
                    face++;
                }
            }

            return edgeStore;
        }
        #endregion

        #region 3d
        private static readonly Matrix4x4 RotateYZ = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, -1, 0, 0), new Vector4(0, 0, 0, 1));
        public static DataDrivenData Build(MeshData meshData, MeshPrismGridOptions meshPrismGridOptions)
        {
            var data = new DataDrivenData
            {
                Cells = new Dictionary<Cell, DataDrivenCellData>(),
                Moves = new Dictionary<(Cell, CellDir), (Cell, CellDir, Connection)>(),
            };

            // First analyse a single layer
            var layerCellData = new Dictionary<Cell, DataDrivenCellData>();
            var layerMoves = new Dictionary<(Cell, CellDir), (Cell, CellDir, Connection)>();
            BuildCellData(meshData, meshPrismGridOptions, layerCellData);
            BuildMoves(meshData, meshPrismGridOptions, layerMoves);

            // Then repeat it on every level
            BuildCellData(meshData, meshPrismGridOptions, layerCellData, data.Cells);
            BuildMoves(meshData, meshPrismGridOptions, layerCellData, layerMoves, data.Moves);

            // TODO: swap Y and Z cell co-ordinates
            return data;
        }

        private static void BuildCellData(MeshData data, MeshPrismGridOptions meshPrismGridOptions, IDictionary<Cell, DataDrivenCellData> layerCellData, IDictionary<Cell, DataDrivenCellData> cellData)
        {
            for (var layer = meshPrismGridOptions.MinLayer; layer < meshPrismGridOptions.MaxLayer; layer++)
            {
                for (var submesh = 0; submesh < data.subMeshCount; submesh++)
                {
                    var face = 0;
                    foreach (var faceIndices in MeshUtils.GetFaces(data, submesh, meshPrismGridOptions.InvertWinding))
                    {
                        // Despite having layerCellData, most stuff needs re-calculating.
                        var cell = new Cell(face, submesh, layer);
                        var deformation = DeformationUtils.GetDeformation(data, meshPrismGridOptions.LayerHeight, meshPrismGridOptions.LayerOffset, meshPrismGridOptions.SmoothNormals, face, layer, submesh, meshPrismGridOptions.InvertWinding);
                        var count = faceIndices.Count;
                        var cellType = PrismInfo.Get(layerCellData[new Cell(face, submesh, 0)].CellType).PrismCellType;
                        var trs = GetTRS(deformation, Vector3.zero);

                        // Transform if necessary
                        if (meshPrismGridOptions.UseXZPlane)
                        {
                            deformation = deformation * RotateYZ;
                            trs = new TRS(trs.ToMatrix() * RotateYZ);
                        }


                        cellData[cell] = new DataDrivenCellData
                        {
                            CellType = cellType,
                            Deformation = deformation,
                            TRS = trs,
                        };
                        face++;
                    }
                }
            }
        }
        private static TRS GetTRS(Deformation deformation, Vector3 p)
        {
            var center = deformation.DeformPoint(p);
            var e = 1e-4f;
            var x = (deformation.DeformPoint(p + Vector3.right * e) - center) / e;
            var y = (deformation.DeformPoint(p + Vector3.up * e) - center) / e;
            var z = (deformation.DeformPoint(p + Vector3.forward * e) - center) / e;
            var m = ToMatrix(x, y, z, new Vector4(center.x, center.y, center.z, 1));

            return new TRS(m);
        }

        // Given a single layer of moves,
        // converts it to moves on multiple layer, in a different cell type
        private static void BuildMoves(
            MeshData data, 
            MeshPrismGridOptions meshPrismGridOptions,
            Dictionary<Cell, DataDrivenCellData> layerCellData,
            Dictionary<(Cell, CellDir), (Cell, CellDir, Connection)> layerMoves,
            IDictionary<(Cell, CellDir), (Cell, CellDir, Connection)> moves)
        {
            for (var layer = meshPrismGridOptions.MinLayer; layer < meshPrismGridOptions.MaxLayer; layer++)
            {
                foreach (var kv in layerMoves)
                {
                    var fromPrismInfo = PrismInfo.Get(layerCellData[kv.Key.Item1].CellType);
                    var toPrismInfo = PrismInfo.Get(layerCellData[kv.Value.Item1].CellType);

                    var fromCell = new Cell(kv.Key.Item1.x, kv.Key.Item1.y, layer);
                    var toCell = new Cell(kv.Value.Item1.x, kv.Value.Item1.y, layer);
                    moves.Add((fromCell, fromPrismInfo.BaseToPrism(kv.Key.Item2)), (toCell, toPrismInfo.BaseToPrism(kv.Value.Item2), kv.Value.Item3));
                }
            }
            foreach (var kv in layerCellData)
            {
                var cellType = kv.Value.CellType;
                var prismInfo = PrismInfo.Get(cellType);
                for (var layer = meshPrismGridOptions.MinLayer; layer < meshPrismGridOptions.MaxLayer; layer++)
                {
                    var cell = new Cell(kv.Key.x, kv.Key.y, layer);
                    if (cell.z < meshPrismGridOptions.MaxLayer - 1)
                    {
                        moves.Add((cell, prismInfo.ForwardDir), (cell + new Vector3Int(0, 0, 1), prismInfo.BackDir, new Connection()));
                    }
                    if (cell.z > meshPrismGridOptions.MinLayer)
                    {
                        moves.Add((cell, prismInfo.BackDir), (cell + new Vector3Int(0, 0, -1), prismInfo.ForwardDir, new Connection()));
                    }
                }
            }
        }

        #endregion

        private static CellDir EdgeIndexToDir(int edgeIndex, int edgeCount, bool doubleOddFaces)
        {
            return (CellDir)(edgeCount % 2 == 1 && doubleOddFaces ? (edgeIndex - 1) * 2 : (edgeIndex - 1));
        }

        private static ICellType GetCellType(int edgeCount, bool doubleOddFaces)
        {
            return edgeCount == 3 && doubleOddFaces ? HexCellType.Get(HexOrientation.FlatTopped) :
                edgeCount == 4 ? SquareCellType.Instance :
                edgeCount % 2 == 1 && doubleOddFaces ? NGonCellType.Get(edgeCount * 2) 
                : NGonCellType.Get(edgeCount);
        }
    }
}
