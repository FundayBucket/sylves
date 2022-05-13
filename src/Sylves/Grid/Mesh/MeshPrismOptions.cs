﻿#if UNITY
#endif


namespace Sylves
{
    public class MeshPrismOptions
    {
        public float LayerHeight { get; set; }
        public float LayerOffset { get; set; }
        public int MinLayer { get; set; }
        public int MaxLayer { get; set; }
        public bool SmoothNormals { get; set; }
        public bool UseXZPlane { get; set; }
    }
}
