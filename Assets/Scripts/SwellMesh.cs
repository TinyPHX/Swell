using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyBox;
using UnityEngine;
using Color = UnityEngine.Color;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Swell
{
    /**
     * @brief Dynamic mesh used to render water surface.
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_mesh.html")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer)), ExecuteInEditMode]
    public class SwellMesh : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startGridSize = 1;
        [SerializeField, Min(0)] private int maxSize = 10;
        [SerializeField, ReadOnly] private float currentSize = 10;
        [SerializeField] private MeshLevel[] levels = {};
        [SerializeField] private bool top = true;
        [SerializeField] private bool bottom = true;
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private List<Vector3> vertices = new();
        private List<Vector2> uv = new();
        private List<Vector3> normals = new();
        private List<int> triangles = new();
        private List<int> triangles2 = new();

        private MeshVectors topMeshVectors = new MeshVectors();
        private MeshVectors bottomMeshVectors = new MeshVectors();
        private bool needsGeneration;
        private float maxLevelSize;

        [Serializable]
        public class  MeshLevel
        {
            [HideInInspector] public string name;
            [SerializeField, Min(1)] private int factor = 1; //factor:1
            [SerializeField, Min(0)] private int count;
            
            private float step;
            private float size;
            private float maxSize;

            public MeshLevel(int factor, int count)
            {
                this.factor = factor;
                this.count = count;
            }

            public MeshLevel(int factor, int count, float step, float size)
            {
                this.factor = factor;
                this.count = count;
                this.step = step;
                this.size = size;
            }

            public void Validate(SwellMesh swellMesh)
            {
                float savedStep = step;
                float savedSize = size;
                float savedMaxSize = maxSize;
                step = GetStep(swellMesh);
                size = GetSize(swellMesh);
                maxSize = GetSize(swellMesh, false);

                int index = Array.IndexOf(swellMesh.Levels, this);
                name = "Level " + (index + 1);
                
                // previous size must be divisible by step with no remainder

                if (index > 0)
                {
                    float previousSize = swellMesh.Levels[index - 1].GetSize(swellMesh, false);
                    if (previousSize % step != 0)
                    {
                        step = savedStep;
                        size = savedSize;
                        maxSize = savedMaxSize;
                        Debug.LogWarning("Invalid SwellMesh size settings for " + swellMesh.gameObject.name + " - " + name);
                        name += " (WARN: Invalid factor)";
                    }
                }
            }

            public bool InBounds(float x, float y)
            {
                x *= 2;
                y *= 2;
                
                return x >= -size && y >= -size && x <= size && y <= size;
            }

            public float Offset => size / 2f;
            public int VertWidth => (int)(size / step) + 1;
            public float Step => step;
            public float Size => size;
            public float MaxSize => maxSize;

            public Vector3 GetOffset(SwellMesh swellMesh)
            {
                float totalSize = GetSize(swellMesh);
                return new (totalSize / 2f, 0, totalSize / 2f);
            }

            private float GetStep(SwellMesh swellMesh)
            {
                return GetCumulativeFactor(swellMesh) * swellMesh.StartGridSize;
            }
            
            private float GetCumulativeFactor(SwellMesh swellMesh)
            {
                int index = Array.IndexOf(swellMesh.Levels, this);
                
                if (index == 0)
                {
                    return factor;
                }
                else
                {
                    return swellMesh.Levels[index - 1].GetCumulativeFactor(swellMesh) * factor;
                }
            }
            
            private float GetSize(SwellMesh swellMesh, bool adjust=true)
            {
                int index = Array.IndexOf(swellMesh.Levels, this);
                float cumulativeFactor = GetCumulativeFactor(swellMesh);
                float currentSize = swellMesh.StartGridSize * cumulativeFactor * (count * 2);
                
                if (index > 0)
                {
                    currentSize += swellMesh.Levels[index - 1].GetSize(swellMesh, adjust);
                }

                while (adjust && currentSize > swellMesh.MaxSize && swellMesh.MaxSize > 0)
                {
                    currentSize -= swellMesh.StartGridSize * cumulativeFactor * 2;
                }

                return currentSize;
            }
        }
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

        void Start()
        {
            GenerateMesh();
        }

        public void MeshLevelValidation()
        {
            levels.ForEach(level => level.Validate(this));
        }

        public void GenerateMesh(float step, int size)
        {
            GenerateMesh(step ,size, top, bottom);
        }

        public void GenerateMesh(float step, int size, bool top, bool bottom)
        {
            levels = new []
            {
                new MeshLevel(1 ,size)
            };
            this.top = top;
            this.bottom = bottom;
            
            GenerateMesh();
        }
        
        [ButtonMethod]
        public void GenerateMesh()
        {
            MeshLevelValidation();
            
            Mesh = new Mesh();
            vertices.Clear();
            uv.Clear();
            normals.Clear();
            triangles.Clear();
            triangles2.Clear();
            topMeshVectors.Clear();
            bottomMeshVectors.Clear();
            Mesh.subMeshCount = 2;

            MeshLevel[] tempLevels = Levels;

            if (tempLevels.Length == 0)
            {
                int size = MaxSize - MaxSize % StartGridSize;
                tempLevels = new[]
                {
                    new MeshLevel(1, size, StartGridSize, size),
                };
            }
            
            currentSize = tempLevels.Last().Size;
            maxLevelSize = tempLevels.Last().MaxSize;

            for (var i = 0; i < tempLevels.Length; i++)
            {
                MeshLevel topLevel = tempLevels[^1];
                MeshLevel innerLevel = i > 0 ? tempLevels[i - 1] : null;
                MeshLevel level = tempLevels[i];
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
            Mesh.SetTriangles(triangles, 0);
            Mesh.SetTriangles(triangles2, 1);

            Mesh.RecalculateBounds();
            Mesh.RecalculateTangents();
        }

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
            float offset = newMeshLevel.Offset;
            int normalDirection = bottom ? -1 : 1;

            if (density <= 0)
            {
                Debug.LogWarning("Swell Mesh not updated. Start Size must be > 0");
                return;
            }
            
            float step = density;
            for (float xi = 0; xi <= meshSize; xi += step)
            {
                for (float yi = 0; yi <= meshSize; yi += step)
                {
                    float x = xi - offset;
                    float y = yi - offset;

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

        public float Size => levels != null && levels.Length > 0 ? levels.Last().Size : MaxSize;

        public MeshLevel[] Levels
        {
            get => levels;
            set
            {
                levels = value;
                MeshLevelValidation();
            }
        }

        public int MaxSize
        {
            get => maxSize;
            set => maxSize = value;
        }

        public int StartGridSize
        {
            get => startGridSize;
            set => startGridSize = value;
        }
        
        private MeshRenderer MeshRenderer
        {
            get
            {
                if (!meshRenderer)
                {
                    meshRenderer = GetComponent<MeshRenderer>();
                }
        
                return meshRenderer;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Selection.transforms.First() == transform && Mesh && Mesh.vertexCount > 0)
            {
                Gizmos.color = Color.grey;
                Gizmos.DrawWireMesh(Mesh, transform.position, transform.rotation, transform.lossyScale);

                if (maxSize > 0)
                {
                    if (maxLevelSize > maxSize)
                    {
                        Gizmos.color = Color.yellow;
                    }
                    else
                    {
                        Gizmos.color = Color.green;
                    }
                    Gizmos.DrawWireCube(transform.position, new Vector3(maxSize, 0, maxSize));
                }
            }
        }
        #endif
    }
}