using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UIElements;
using Color = UnityEngine.Color;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Swell
{
    /**
     * @brief Dynamic mesh used to render water surface.
     */
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwellMesh : MonoBehaviour
    {
        /**
         * @brief Represents mesh grid fidelity levels.
         */
        [Serializable]
        public class  MeshLevel
        {
            [SerializeField] private float step;
            [SerializeField] private float size;

            public MeshLevel(float step, float size)
            {
                this.step = step;
                this.size = size;
            }

            public bool InBounds(float x, float y)
            {
                x *= 2;
                y *= 2;
                
                return x >= -size && y >= -size && x <= size && y <= size;
            }

            // public int VertWidth => (int)(size + 1);
            // public int VertHeight => (int)(size + 1);
            // public int VertCount => VertWidth * VertHeight;
            public Vector3 Offset => new (size / 2f, 0, size / 2f);
            public int VertWidth => (int)(size / step) + 1;
            public float Step => step;
            public float Size => size;
        }
 
        [SerializeField] private MeshLevel[] levels = new []
        {
            new MeshLevel(2,16), 
            new MeshLevel(4, 32),
            new MeshLevel(8, 64),
            new MeshLevel(16, 128),
            new MeshLevel(32,256), 
            new MeshLevel(64, 512),
            new MeshLevel(128, 1024),
            new MeshLevel(256, 2048),
        };
        [SerializeField] private bool top = true;
        [SerializeField] private bool bottom = true;
        // [SerializeField] private bool splitTopAndBottom = false; //Add ability to apply different materials
        
        private MeshFilter meshFilter;
        private List<Vector3> vertices = new();
        private List<Vector2> uv = new();
        private List<Vector3> normals = new();
        private List<int> triangles = new();
        private List<int> triangles2 = new();

        private class MeshVectors
        {
            public Dictionary<(float, float), int> positionToIndex = new();
            public Dictionary<float, List<float>> xByY = new();
            public Dictionary<float, List<float>> yByX = new();

            public void Clear()
            {
                xByY.Clear();
                yByX.Clear();
                positionToIndex.Clear();
            }
        }

        private MeshVectors topMeshVectors = new MeshVectors();
        private MeshVectors bottomMeshVectors = new MeshVectors();

        void Start()
        {
            GenerateMesh();
        }

        public void GenerateMesh(float step, int size)
        {
            GenerateMesh(step ,size, top, bottom);
        }

        public void GenerateMesh(float step, int size, bool top, bool bottom)
        {
            levels = new []
            {
                new MeshLevel(step ,size)
            };
            this.top = top;
            this.bottom = bottom;
            
            GenerateMesh();
        }

        public void GenerateMesh()
        {
            Mesh = new Mesh();
            vertices.Clear();
            uv.Clear();
            normals.Clear();
            triangles.Clear();
            triangles2.Clear();
            topMeshVectors.Clear();
            bottomMeshVectors.Clear();
            Mesh.subMeshCount = 2;
            // vertices = new();
            // uv = new();
            // normals = new();
            // triangles = new();

            for (var i = 0; i < levels.Length; i++)
            {
                MeshLevel topLevel = levels[^1];
                MeshLevel innerLevel = i > 0 ? levels[i - 1] : null;
                MeshLevel level = levels[i];
                if (top)
                {
                    AddToMesh(level, topLevel, innerLevel);
                }

                if (bottom)
                {
                    AddToMesh(level, topLevel, innerLevel, true);
                }
            }

            Mesh.vertices = vertices.ToArray();
            Mesh.uv = uv.ToArray();
            Mesh.normals = normals.ToArray();
            // Mesh.triangles = triangles.ToArray();
            Mesh.SetTriangles(triangles, 0);
            Mesh.SetTriangles(triangles2, 1);

            Mesh.RecalculateBounds();
            Mesh.RecalculateTangents();
        }

        // public void GenerateMesh()
        // {
        //     levels = new[]
        //     {
        //         new MeshLevel(1, 5),
        //         new MeshLevel(1, 20),
        //     };
        //     
        //     MeshLevel level = levels[0];
        //     int sides = Convert.ToInt32(bottom) + Convert.ToInt32(top);
        //     int arrayLength = level.VertCount * sides;
        //     
        //     Mesh = new Mesh();
        //     Mesh.vertices = new Vector3[arrayLength];
        //     Mesh.uv = new Vector2[arrayLength];
        //     Mesh.normals = new Vector3[arrayLength];
        //     Mesh.triangles = new int[(arrayLength - (level.VertWidth + level.VertHeight - 1) * sides) * 2 * 3]; // number of triangles per grid (2) times 3 (one for each corner)
        //     
        //     vertIndex = -1;
        //     triCount = 0;
        //     
        //     if (top)
        //     {
        //         AddToMesh(levels[0]);
        //     }
        //     if (bottom)
        //     {
        //         AddToMesh(levels[0], true);
        //     }
        //     
        //     Mesh.RecalculateBounds();
        // }

        // private void AddAllToMesh()
        // {
        //     int density = 2;
        //     int[] levelWidth = { 10, 20 };
        //     int[] levelFactor = { 1, 2 };
        //     int levels = levelWidth.Length;
        //     
        //     Mesh = new Mesh();
        //     List<Vector3> vertices = new List<Vector3>();
        //     List<Vector2> uv = new List<Vector2>();
        //     List<Vector3> normals = new List<Vector3>();
        //     List<int> triangles = new List<int>();
        //     
        //     int width = levelWidth[levels] + 1;
        //     int height = levelWidth[levels] + 1;
        //     Vector3 offset = new Vector3((width - 1) / 2f, 0, (height - 1) / 2f)
        //     int normalDirection = bottom ? -1 : 1;
        //
        //     for (int x = 0; x < width; x++)
        //     {
        //         for (int y = 0; y < height; y++)
        //         {
        //             vertices.Add(new Vector3(x, 0, y) - offset);
        //             uv.Add(new Vector2(x / (float)(width - 1),  y / (float)(height - 1)));
        //             normals.Add(new Vector3(0, normalDirection, 0));
        //     
        //             if (x > 0 && y > 0)
        //             {
        //                 int[] p =
        //                 {
        //                     vertIndex + 1 + width * (y - 1) + (x - 1),
        //                     vertIndex + 1 + width * (y - 0) + (x - 1),
        //                     vertIndex + 1 + width * (y - 1) + (x - 0),
        //                     vertIndex + 1 + width * (y - 0) + (x - 0)
        //                 };
        //     
        //                 List<int> triOrder = new List<int>() { 0, 1, 2, 1, 3, 2 };
        //     
        //                 if (bottom)
        //                 {
        //                     triOrder.Reverse();
        //                 }
        //     
        //                 foreach (int triIndex in triOrder)
        //                 {
        //                     triangles.Add(p[triIndex]);
        //                 }
        //             }
        //         }
        //     }
        //
        //     Mesh.vertices = vertices.ToArray();
        //     Mesh.uv = uv.ToArray();
        //     Mesh.normals = normals.ToArray();
        //     Mesh.triangles = triangles.ToArray();
        // }

        private bool AddVector(float x, float y, int index, bool bottom=false)
        {
            MeshVectors meshVectors = bottom ? bottomMeshVectors : topMeshVectors;
            
            if (meshVectors.positionToIndex.TryAdd((x, y), index))
            {
                if (!meshVectors.xByY.ContainsKey(y))
                {
                    meshVectors.xByY.Add(y, new List<float>());
                }
                if (!meshVectors.yByX.ContainsKey(x))
                {
                    meshVectors.yByX.Add(x, new List<float>());
                }
                
                meshVectors.xByY[y].Add(x);
                meshVectors.yByX[x].Add(y);

                return true;
            }

            return false;
        }

        private List<int> GetPointsInSquare(float x1, float y1, float x2, float y2, bool bottom)
        {
            MeshVectors meshVectors = bottom ? bottomMeshVectors : topMeshVectors;
            
            List<(float, float)> points = new List<(float, float)>();
            List<int> pointIndexes = new List<int>();
            float xMin = Mathf.Min(x1, x2);
            float xMax = Mathf.Max(x1, x2);
            float yMin = Mathf.Min(y1, y2);
            float yMax = Mathf.Max(y1, y2);
            
            var side1 = meshVectors.yByX[xMin].Where(y => y > yMin && y <= yMax).OrderBy(y => y);
            var side2 = meshVectors.xByY[yMax].Where(x => x > xMin && x <= xMax).OrderBy(x => x);
            var side3 = meshVectors.yByX[xMax].Where(y => y >= yMin && y < yMax).OrderBy(y => -y);
            var side4 = meshVectors.xByY[yMin].Where(x => x >= xMin && x < xMax).OrderBy(x => -x);

            // Get all points in clockwise order around the square.
            foreach (float y in side1) { points.Add((xMin, y)); }
            foreach (float x in side2) { points.Add((x, yMax)); }
            foreach (float y in side3) { points.Add((xMax, y)); }
            foreach (float x in side4) { points.Add((x, yMin)); }
            
            //Find any corner that has no midpoint on any side
            int lonelyCorner = -1;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var adjacentPointsY = meshVectors.xByY[point.Item2].Where(x => x >= xMin && x <= xMax);
                var adjacentPointsX = meshVectors.yByX[point.Item1].Where(y => y >= yMin && y <= yMax);

                if (adjacentPointsX.Count() == 2 && adjacentPointsY.Count() == 2)
                {
                    lonelyCorner = i;
                    break;
                }
            }
            //Make lonely corner the start index for applying tris.
            for (var i = 0; i < points.Count; i++)
            {
                int adjustedI = (i + lonelyCorner) % points.Count;
                pointIndexes.Add(meshVectors.positionToIndex[points[adjustedI]]);
            }

            return pointIndexes;
        }

        private void AddToMesh(MeshLevel newMeshLevel, MeshLevel topLevel, MeshLevel innerLevel = null, bool bottom = false)
        {
            float meshSize = newMeshLevel.Size;
            float topSize = topLevel.Size;
            float innerDistance = (innerLevel?.Size) / 2 ?? 0;
            float density = newMeshLevel.Step;
            Vector3 offset = newMeshLevel.Offset;  //new Vector3((width - 1) / 2f, 0, (height - 1) / 2f)
            int normalDirection = bottom ? -1 : 1;

            float step = density;
            for (float xi = 0; xi <= meshSize; xi += step)
            {
                for (float yi = 0; yi <= meshSize; yi += step)
                {
                    float x = xi - offset.x;
                    float y = yi - offset.z;

                    if (x <= -innerDistance || y <= -innerDistance || x > innerDistance || y > innerDistance)
                    {
                        if (AddVector(x, y, vertices.Count, bottom))
                        {
                            vertices.Add(new Vector3(x, 0, y));
                            uv.Add(new Vector2((xi + (topSize - meshSize) / 2) / topSize, (yi + (topSize - meshSize) / 2) / topSize));
                            normals.Add(new Vector3(0, normalDirection, 0));
                        }

                        if (xi > 0 && yi > 0)
                        {
                            List<int> p = GetPointsInSquare(x, y, x - step, y - step, bottom);
                            List<int> triOrder = new List<int>() { };
                            for (int ti = 0; ti < p.Count() - 1; ti++)
                            {
                                triOrder.Add(0);
                                triOrder.Add(ti);
                                triOrder.Add(ti + 1);
                            }

                            List<int> trianglesToUpdate = triangles;
                            if (bottom)
                            {
                                triOrder.Reverse();
                                trianglesToUpdate = triangles2;
                            }
                            
                            foreach (int triIndex in triOrder)
                            {
                                trianglesToUpdate.Add(p[triIndex]);
                            }
                        }
                    }
                }
            }
        }

        // private void AddToMesh(MeshLevel newMeshLevel, bool bottom = false)
        // {
        //     Vector3[] vertices = Mesh.vertices;
        //     Vector2[] uv = Mesh.uv;
        //     Vector3[] normals = Mesh.normals;
        //     int[] triangles = Mesh.triangles;
        //     
        //     int width = newMeshLevel.VertWidth;
        //     int height = newMeshLevel.VertWidth;
        //     float density = newMeshLevel.Density;
        //     Vector3 offset = newMeshLevel.Offset;  //new Vector3((width - 1) / 2f, 0, (height - 1) / 2f)
        //     int normalDirection = bottom ? -1 : 1;
        //
        //     int newVertIndex = 0;
        //
        //     for (int x = 0; x < width; x++)
        //     {
        //         for (int y = 0; y < height; y++)
        //         {
        //             newVertIndex = vertIndex + 1 + width * y + x;
        //     
        //             vertices[newVertIndex] = new Vector3(x, 0, y) - offset;
        //             uv[newVertIndex] = new Vector2(x / (float)(width - 1),  y / (float)(height - 1));
        //             normals[newVertIndex] = new Vector3(0, normalDirection, 0);
        //     
        //             if (x > 0 && y > 0)
        //             {
        //                 int[] p =
        //                 {
        //                     vertIndex + 1 + width * (y - 1) + (x - 1),
        //                     vertIndex + 1 + width * (y - 0) + (x - 1),
        //                     vertIndex + 1 + width * (y - 1) + (x - 0),
        //                     vertIndex + 1 + width * (y - 0) + (x - 0)
        //                 };
        //     
        //                 List<int> triOrder = new List<int>() { 0, 1, 2, 1, 3, 2 };
        //     
        //                 if (bottom)
        //                 {
        //                     triOrder.Reverse();
        //                 }
        //     
        //                 foreach (int triIndex in triOrder)
        //                 {
        //                     triangles[triCount++] = p[triIndex];
        //                 }
        //             }
        //         }
        //     }
        //
        //     vertIndex = newVertIndex;
        //
        //     Mesh.vertices = vertices;
        //     Mesh.uv = uv;
        //     Mesh.normals = normals;
        //     Mesh.triangles = triangles;
        // }

        private string Strigify<T>(T[] array)
        {
            StringBuilder stringBuilder = new StringBuilder(); 
            foreach(var item in array)
            {
                stringBuilder.Append(item.ToString()).Append("\n");
            }

            return stringBuilder.ToString();
        }
        
        public Mesh Mesh
        {
            get
            {
                if (!meshFilter)
                {
                    meshFilter = GetComponent<MeshFilter>();
                }
                
                if (Application.isPlaying)
                {
                    return meshFilter.mesh;
                }
                else
                {
                    return meshFilter.sharedMesh;
                }
            }
            private set
            {
                if (!meshFilter)
                {
                    meshFilter = GetComponent<MeshFilter>();
                }
                
                if (Application.isPlaying)
                {
                    meshFilter.mesh = value;
                }
                else
                {
                    meshFilter.sharedMesh = value;
                }
            }
        }

        public float Size => levels.Last().Size;

        public MeshLevel[] Levels
        {
            get => levels;
            set => levels = value;
        }
        
        #if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Selection.transforms.First() == this.transform)
            {
                Gizmos.color = Color.grey;
                Gizmos.DrawWireMesh(Mesh, transform.position, transform.rotation, transform.lossyScale);
            }
        }
        #endif
    }
}