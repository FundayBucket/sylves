﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Sylves
{
    public class Prototile
	{
		public string Name { get; set; }

		public (Matrix4x4 transform, string childName)[] ChildPrototiles { get; set; }

		public (int fromChild, int fromChildSide, int toChild, int toChildSide)[] InteriorPrototileAdjacencies { get; set; }

		public (int parentSide, int parentSubSide, int parentSubSideCount, int child, int childSide)[] ExteriorPrototileAdjacencies { get; set; }


		public Vector3[][] ChildTiles { get; set; }

        public (int fromChild, int fromChildSide, int toChild, int toChildSide)[] InteriorTileAdjacencies { get; set; }

        public (int parentSide, int parentSubSide, int parentSubSideCount, int child, int childSide)[] ExteriorTileAdjacencies { get; set; }

		public Prototile CopyPrototileToTiles()
		{
			var r = Clone();
			r.InteriorTileAdjacencies = r.InteriorPrototileAdjacencies;
			r.ExteriorTileAdjacencies = r.ExteriorPrototileAdjacencies;
			return r;
		}

		public Prototile RenameChildren(Dictionary<string, string> renames)
		{
			var r = Clone();
			r.ChildPrototiles = ChildPrototiles
				.Select(t => (t.transform, renames[t.childName]))
				.ToArray();
			return r;
		}

        public Prototile SwapChildren(int a, int b)
        {
            int Update(int c) => c == a ? b : c == b ? a : c;
            var r = Clone();
            r.ChildPrototiles = ((Matrix4x4, string)[])r.ChildPrototiles.Clone();
            r.ChildPrototiles[a] = ChildPrototiles[b];
            r.ChildPrototiles[b] = ChildPrototiles[a];
            r.InteriorPrototileAdjacencies = InteriorPrototileAdjacencies.Select(t =>
            (Update(t.fromChild), t.fromChildSide, Update(t.toChild), t.toChildSide)).ToArray();
            r.ExteriorPrototileAdjacencies = ExteriorPrototileAdjacencies.Select(t =>
            (t.parentSide, t.parentSubSide, t.parentSubSideCount, Update(t.child), t.childSide)).ToArray();
            return r;
        }

		public Prototile Rename(string name)
		{
			var r = Clone();
			r.Name = name;
			return r;
		}

		public Prototile Clone()
		{
			return (Prototile)MemberwiseClone();
		}

    }
    /*
    internal struct Path
    {
        public uint X;
        public uint Y;
        public uint Z;
        public int TileBits;
        public int ProtoTileBits;

        public Path(Cell cell, int tileBits, int protoTileBits)
        {
            X = (uint)cell.x;
            Y = (uint)cell.y;
            Z = (uint)cell.z;
            TileBits = tileBits;
            ProtoTileBits = protoTileBits;
        }

        public int Tile => (int)(X & ((1 << TileBits) - 1));

        public int MaxHeight
        {
            get
            {
                var pathBitsAvailable = (Z != 0 ? 3 : Y != 0 ? 2 : 1) * 32 - TileBits;
                return pathBitsAvailable / ProtoTileBits;
            }
        }

        public List<int> GetPath()
        {
            var l = GetPathInternal().ToList();
            while(l.Count > 0 && l[l.Count-1] == 0)
            {
                l.RemoveAt(l.Count - 1);
            }
        }

        private IEnumerable<int> GetPathInternal()
        {
            if (ProtoTileBits == 0)
                yield break;
            int pos = 1;
            ulong current = X;
            int bits = 32;
            current = current >> TileBits;
            bits -= TileBits;
            while(true)
            {
                if(bits < ProtoTileBits)
                {
                    switch(pos)
                    {
                        case 1:
                            current = current | (Y << bits);
                            bits += 32;
                            pos += 1;
                            break;
                        case 2:
                            current = current | (Z << bits);
                            bits += 32;
                            pos += 1;
                            break;
                        case 3:
                            yield break;
                    }
                }
                yield return (int)(current & (((ulong)1 << ProtoTileBits) - 1));
            }
        }
    }*/

    public class SubstitutionTilingGrid : IGrid
	{
        private readonly Prototile[] prototiles;
        private readonly Dictionary<string, Prototile> prototilesByName;
        private int tileBits;
        private int prototileBits;
        private List<ICellType> cellTypes;
        Func<int, string> hierarchy;

        public SubstitutionTilingGrid(Prototile[] prototiles, string[] hierarchy):
            this(prototiles, i => hierarchy[i % hierarchy.Length])
        {

        }

        public SubstitutionTilingGrid(Prototile[] prototiles, Func<int, string> hierarchy)
		{
            this.prototiles = prototiles;
            prototilesByName = prototiles.ToDictionary(x => x.Name);
            var maxTileCount = prototiles.Max(x => x.ChildTiles?.Length ?? 0);
            tileBits = (int)Math.Ceiling(Math.Log(maxTileCount) / Math.Log(2));
            var maxPrototileCount = prototiles.Max(x => x.ChildPrototiles?.Length ?? 0);
            prototileBits = (int)Math.Ceiling(Math.Log(maxPrototileCount) / Math.Log(2));
            this.hierarchy = hierarchy;

            cellTypes = prototiles.SelectMany(x => x.ChildTiles.Select(y => y.Length)).Distinct().Select(NGonCellType.Get).ToList();
        }

        internal (int childTile, List<int> path) Parse(Cell cell)
        {
            ulong current = (uint)cell.x;
            var childTile = (int)(current & ((1UL << tileBits) - 1));



            if (prototileBits == 0)
                return (childTile, new List<int>());

            var path = new List<int>();
            int pos = 1;
            int bits = 32;
            current = current >> tileBits;
            bits -= tileBits;
            while (true)
            {
                if (bits < prototileBits)
                {
                    if (pos == 3)
                        break;
                    switch (pos)
                    {
                        case 1:
                            current = current | ((uint)cell.y << bits);
                            bits += 32;
                            pos += 1;
                            break;
                        case 2:
                            current = current | ((uint)cell.z << bits);
                            bits += 32;
                            pos += 1;
                            break;
                    }
                }
                path.Add((int)(current & (((ulong)1 << prototileBits) - 1)));
                current = current >> prototileBits;
                bits -= prototileBits;
            }
            while (path.Count > 0 && path[path.Count - 1] == 0)
                path.RemoveAt(path.Count - 1);
            return (childTile, path);
        }

        internal Cell Format(int childTile, List<int> path)
        {
            var cell = new Cell();
            ulong current = 0;
            var bits = 0;
            var pos = 1;

            current = (ulong)childTile;
            bits += tileBits;

            foreach(var p in path)
            {
                current = current | ((ulong)p << bits);
                bits += prototileBits;
                if(bits >= 32)
                {
                    switch(pos)
                    {
                        case 1:
                            cell.x = (int)(current & 0xFFFFFFFF);
                            current = current >> 32;
                            bits -= 32;
                            pos++;
                            break;
                        case 2:
                            cell.y = (int)(current & 0xFFFFFFFF);
                            current = current >> 32;
                            bits -= 32;
                            pos++;
                            break;
                        default:
                            throw new Exception();
                    }
                }
            }

            switch (pos)
            {
                case 1:
                    cell.x = (int)(current & 0xFFFFFFFF);
                    break;
                case 2:
                    cell.y = (int)(current & 0xFFFFFFFF);
                    break;
                default:
                    cell.z = (int)(current & 0xFFFFFFFF);
                    break;
            }

            return cell;

        }



        private (string prototile, Matrix4x4 prototileTransform, int childTile) LocateCell(Cell cell)
        {
            var (childTile, path) = Parse(cell);
            var transform = Matrix4x4.identity;
            string parent = hierarchy(0);
            for (var i = 0; i < path.Count; i++)
            {
                parent = hierarchy(i + 1);
                transform = transform * prototilesByName[parent].ChildPrototiles[0].transform.inverse;
            }
            for (var i = path.Count - 1; i >= 0; i--)
            {
                var t = prototilesByName[parent].ChildPrototiles[path[i]];
                parent = t.childName;
                transform = transform * t.transform;
            }
            return (parent, transform, childTile);
        }

        /// <summary>
        /// Returns n+1 parent elements for a path of length n
        /// </summary>
        private IList<Prototile> Parents(List<int> path)
        {
            var parents = new List<Prototile>();
            string parent = hierarchy(0);
            for (var i = 0; i < path.Count; i++)
            {
                parent = hierarchy(i + 1);
            }
            parents.Add(prototilesByName[parent]);
            for (var i = path.Count - 1; i >= 0; i--)
            {
                var t = prototilesByName[parent].ChildPrototiles[path[i]];
                parent = t.childName;
                parents.Insert(0, prototilesByName[parent]);
            }
            return parents;
        }


        #region Basics
        public bool Is2d => true;

        public bool Is3d => false;

        public bool IsPlanar => true;

        public bool IsRepeating => false;

        public bool IsOrientable => true;

        public bool IsFinite => false;

        public bool IsSingleCellType => cellTypes.Count == 1;

        public int CoordinateDimension => 3;

        public IEnumerable<ICellType> GetCellTypes() => cellTypes;
        #endregion

        #region Relatives
        public IGrid Unbounded => throw new NotImplementedException();

        public IGrid Unwrapped => throw new NotImplementedException();

        public virtual IDualMapping GetDual() => throw new NotImplementedException();

        #endregion

        #region Cell info

        public IEnumerable<Cell> GetCells() => throw new GridInfiniteException();

        public ICellType GetCellType(Cell cell) => throw new NotImplementedException();

        public bool IsCellInGrid(Cell cell) => throw new NotImplementedException();
        #endregion

        #region Topology

        public bool TryMove(Cell cell, CellDir dir, out Cell dest, out CellDir inverseDir, out Connection connection)
        {
            var (childTile, path) = Parse(cell);
            int GetPathItem(int height) => height < path.Count ? path[height] : 0;
            var parents = Parents(path);
            Prototile GetParent(int height) => height < parents.Count ? parents[height] : prototilesByName[hierarchy(height)];
            var prototile = GetParent(0);
            var tileInterior = prototile.InteriorTileAdjacencies
                .Where(x => x.fromChild == childTile && x.fromChildSide == (int)dir)
                .ToList();
            if (tileInterior.Count == 1)
            {
                dest = Format(tileInterior[0].toChild, path);
                inverseDir = (CellDir)tileInterior[0].toChildSide;
                connection = new Connection();
                return true;
            }
            else
            {
                var tileExterior = prototile.ExteriorTileAdjacencies
                    .Where(x => x.child == childTile && x.childSide == (int)dir)
                    .Single();


                (List<int> partialPath, int side, Prototile otherParent) TryMovePrototile(int height, int childPrototile, int prototileSide)
                {
                    var parent = GetParent(height + 1);
                    var interior = parent.InteriorPrototileAdjacencies
                        .Where(x => x.fromChild == childPrototile && x.fromChildSide == prototileSide)
                        .ToList();
                    if (interior.Count == 1)
                    {
                        var partialPath = path.Take(height + 1).ToList();
                        var toChild = interior[0].toChild;
                        partialPath.Insert(0, toChild);
                        return (partialPath, interior[0].toChildSide, prototilesByName[parent.ChildPrototiles[toChild].childName]);
                    }
                    else
                    {
                        var exterior = parent.ExteriorPrototileAdjacencies
                            .Where(x => x.child == childPrototile && x.childSide == prototileSide)
                            .Single();

                        var (partialPath, otherSide, otherParent) = TryMovePrototile(height + 1, GetPathItem(height + 1), exterior.parentSide);

                        var otherSubside = exterior.parentSubSideCount - 1 - exterior.parentSubSide;
                        var otherExterior = otherParent.ExteriorPrototileAdjacencies
                            .Where(x => x.parentSide == otherSide && x.parentSubSide == otherSubside)
                            .Single();
                        if (otherExterior.parentSubSideCount != exterior.parentSubSideCount)
                            throw new Exception();
                        partialPath.Insert(0, otherExterior.child);
                        return (partialPath, otherExterior.childSide, prototilesByName[otherParent.ChildPrototiles[otherExterior.child].childName]);
                    }
                }

                {
                    var (partialPath, otherSide, otherParent) = TryMovePrototile(0, GetPathItem(0), tileExterior.parentSide);


                    var otherSubside = tileExterior.parentSubSideCount - 1 - tileExterior.parentSubSide;
                    var otherExterior = otherParent.ExteriorTileAdjacencies
                        .Where(x => x.parentSide == otherSide && x.parentSubSide == otherSubside)
                        .Single();
                    if (otherExterior.parentSubSideCount != tileExterior.parentSubSideCount)
                        throw new Exception();
                    //partialPath.Insert(0, otherExterior.child);
                    //return (partialPath, otherExterior.childSide, prototilesByName[otherParent.ChildPrototiles[otherExterior.child].childName]);

                    dest = Format(otherExterior.child, partialPath);
                    inverseDir = (CellDir)otherExterior.childSide;
                    connection = default;
                    return true;
                }
            }
        }

        public bool TryMoveByOffset(Cell startCell, Vector3Int startOffset, Vector3Int destOffset, CellRotation startRotation, out Cell destCell, out CellRotation destRotation) => throw new NotImplementedException();


        public bool ParallelTransport(IGrid aGrid, Cell aSrcCell, Cell aDestCell, Cell srcCell, CellRotation startRotation, out Cell destCell, out CellRotation destRotation) => throw new NotImplementedException();


        public IEnumerable<CellDir> GetCellDirs(Cell cell) => throw new NotImplementedException();

        public IEnumerable<CellCorner> GetCellCorners(Cell cell) => throw new NotImplementedException();

        public IEnumerable<(Cell, CellDir)> FindBasicPath(Cell startCell, Cell destCell) => throw new NotImplementedException();

        #endregion

        #region Index
        public int IndexCount => throw new NotImplementedException();

        public int GetIndex(Cell cell) => throw new NotImplementedException();

        public Cell GetCellByIndex(int index) => throw new NotImplementedException();
        #endregion

        #region Bounds
        public IBound GetBound() => throw new NotImplementedException();

        public IBound GetBound(IEnumerable<Cell> cells) => throw new NotImplementedException();

        public IGrid BoundBy(IBound bound) => throw new NotImplementedException();

        public IBound IntersectBounds(IBound bound, IBound other) => throw new NotImplementedException();

        public IBound UnionBounds(IBound bound, IBound other) => throw new NotImplementedException();

        public IEnumerable<Cell> GetCellsInBounds(IBound bound) => throw new NotImplementedException();

        public bool IsCellInBound(Cell cell, IBound bound) => throw new NotImplementedException();
        #endregion

        #region Position
        public Vector3 GetCellCenter(Cell cell)
        {
            // TODO: More efficient
            GetPolygon(cell, out var vertices, out var transform);
            return vertices.Select(transform.MultiplyPoint3x4).Aggregate((x, y) => x + y) / vertices.Length;
        }

        public Vector3 GetCellCorner(Cell cell, CellCorner cellCorner) => throw new NotImplementedException();

        public TRS GetTRS(Cell cell) => throw new NotImplementedException();

        #endregion

        #region Shape
        public Deformation GetDeformation(Cell cell) => throw new NotImplementedException();

        public void GetPolygon(Cell cell, out Vector3[] vertices, out Matrix4x4 transform)
        {
            var (prototile, prototileTransform, childTile) = LocateCell(cell);
            vertices = prototilesByName[prototile].ChildTiles[childTile];
            transform = prototileTransform;
        }

        public IEnumerable<(Vector3, Vector3, Vector3, CellDir)> GetTriangleMesh(Cell cell) => throw new NotImplementedException();

        public void GetMeshData(Cell cell, out MeshData meshData, out Matrix4x4 transform) => throw new NotImplementedException();

        #endregion

        #region Query
        public bool FindCell(Vector3 position, out Cell cell) => throw new NotImplementedException();

        public bool FindCell(
            Matrix4x4 matrix,
            out Cell cell,
            out CellRotation rotation) => throw new NotImplementedException();

        public IEnumerable<Cell> GetCellsIntersectsApprox(Vector3 min, Vector3 max) => throw new NotImplementedException();

        public IEnumerable<RaycastInfo> Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity) => throw new NotImplementedException();
        #endregion

        #region Symmetry
        public GridSymmetry FindGridSymmetry(ISet<Cell> src, ISet<Cell> dest, Cell srcCell, CellRotation cellRotation) => throw new NotImplementedException();

        public bool TryApplySymmetry(GridSymmetry s, IBound srcBound, out IBound destBound) => throw new NotImplementedException();

        public bool TryApplySymmetry(GridSymmetry s, Cell src, out Cell dest, out CellRotation r) => throw new NotImplementedException();
        #endregion
    }
}

