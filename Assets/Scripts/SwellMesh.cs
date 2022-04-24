using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyBox;
using UnityEngine;
using Color = UnityEngine.Color;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Analytics;
using UnityEngine.UIElements;
#endif

namespace Swell
{
    /**
     * @brief Dynamic mesh used to render water surface.
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_mesh.html")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwellMesh : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startGridSize = 1;
        [SerializeField, Min(0)] private int maxSize = 10;
        [SerializeField, ReadOnly] private float currentSize = 10;
        [SerializeField] private MeshLevel[] levels = {};
        [SerializeField] private bool top = true;
        [SerializeField] private bool bottom = true;

        private SwellWater water;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshLevel[] usedLevels = {};
        private MeshLevel[] defaultLevels = new[] { new MeshLevel(1, 10, 1, 10) };
        private List<Vector3> vertices = new();
        private List<Vector2> uv = new();
        private List<Vector3> normals = new();
        private List<int> triangles = new();
        private List<int> triangles2 = new();

        private MeshVectors topMeshVectors = new ();
        private MeshVectors bottomMeshVectors = new ();
        private float maxLevelSize;
        
        //Previous state
        private MeshLevel[] previousLevels;
        private float previousStartGridSize;
        private float previousMaxSize;
        private bool previousTop;
        private bool previousBottom;

        [Serializable]
        public class  MeshLevel
        {
            [HideInInspector] public string name;
            [SerializeField, Min(1)] private int factor;
            [SerializeField, Min(0)] private int count;
            
            private float step;
            private float size;
            private float maxSize;
            private int index;

            public MeshLevel(MeshLevel level)
            {
                this.factor = level.factor;
                this.count = level.count;
            }

            public MeshLevel(int factor, int count)
            {
                this.factor = factor;
                this.count = count;
            }

            public override bool Equals(object obj)
            {
                if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                {
                    return false;
                }
                else
                {
                    MeshLevel mesh = (MeshLevel)obj;
                    return mesh.factor == factor && mesh.count == count && mesh.index == index;
                }
            }

            public void SetDefaults(int step, int size)
            {
                this.factor = 1;
                this.count = count / step;
                this.size = size;
                this.step = step;
            }

            public MeshLevel(int factor, int count, float step, float size)
            {
                this.factor = factor;
                this.count = count;
                this.step = step;
                this.size = size;
            }

            public bool Validate(SwellMesh swellMesh)
            {
                bool valid = true;

                if (factor <= 0)
                {
                    factor = 1;
                }
                
                float savedStep = step;
                float savedSize = size;
                float savedMaxSize = maxSize;
                index = Array.IndexOf(swellMesh.levels, this);
                
                step = GetStep(swellMesh);
                size = GetSize(swellMesh);
                maxSize = GetSize(swellMesh, false);

                name = "Level " + (index + 1);
                
                // previous size must be divisible by step with no remainder
                if (PreviousLevel(swellMesh) != null)
                {
                    float previousSize = PreviousLevel(swellMesh).GetSize(swellMesh, false);
                    valid &= previousSize % step == 0;
                }
                
                if (!valid)
                {
                    step = savedStep;
                    size = savedSize;
                    maxSize = savedMaxSize;
                    Debug.LogWarning("Invalid SwellMesh size settings for " + swellMesh.gameObject.name + " - " + name);
                    name += " (WARN: Invalid factor)";
                }

                return valid;
            }

            public bool InBounds(float x, float y)
            {
                x *= 2;
                y *= 2;

                const float forgiveness = .0001f;
                
                return x + forgiveness >= -size && y + forgiveness >= -size && x - forgiveness <= size && y - forgiveness <= size;
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

            private MeshLevel PreviousLevel(SwellMesh swellMesh)
            {
                if (index > 0)
                {
                    return swellMesh.levels[index - 1];
                }

                return null;
            }

            private float GetStep(SwellMesh swellMesh)
            {
                return GetCumulativeFactor(swellMesh) * swellMesh.StartGridSize;
            }
            
            private float GetCumulativeFactor(SwellMesh swellMesh)
            {
                return factor * (PreviousLevel(swellMesh)?.GetCumulativeFactor(swellMesh) ?? 1);
            }
            
            private float GetSize(SwellMesh swellMesh, bool adjust=true)
            {
                float cumulativeFactor = GetCumulativeFactor(swellMesh);
                float currentSize = swellMesh.StartGridSize * cumulativeFactor * (count * 2);
                
                currentSize += PreviousLevel(swellMesh)?.GetSize(swellMesh, adjust) ?? 0;

                while (adjust && currentSize > swellMesh.MaxSize && swellMesh.MaxSize > 0)
                {
                    currentSize -= swellMesh.StartGridSize * cumulativeFactor * 2;
                }

                return currentSize;
            }
        }
        
        [Serializable]
        private class MeshVectors
        {
            public Dictionary<(float, float), int> positionToIndex = new();
            public Dictionary<float, List<float>> xByY = new();
            public Dictionary<float, List<float>> yByX = new();

            public void Clear()
            {
                positionToIndex.Clear();
                xByY.Clear();
                yByX.Clear();
            }

            public bool AddVector(float x, float y, int index)
            {
                if (positionToIndex.TryAdd((x, y), index))
                {
                    if (!xByY.ContainsKey(y))
                    {
                        xByY.Add(y, new List<float>());
                    }
                    if (!yByX.ContainsKey(x))
                    {
                        yByX.Add(x, new List<float>());
                    }
                
                    xByY[y].Add(x);
                    yByX[x].Add(y);

                    return true;
                }

                return false;
            }
        }

        void Start()
        {
            GenerateMesh();
        }

        public void Update()
        {
            UpdateLevels();
            MeshLevelValidation();
        }

        private void UpdateLevels()
        {
            if (levels.Length == 0)
            {
                int size = MaxSize - MaxSize % (StartGridSize == 0 ? 1 : StartGridSize);
                defaultLevels[0].SetDefaults(StartGridSize, size);
                usedLevels = defaultLevels;
            }
            else if (MeshLevelValidation())
            {
                usedLevels = levels;
            }
        }

        private bool MeshLevelValidation()
        {
            bool valid = true;
            levels.ForEach(level => valid &= level.Validate(this));
            return valid;
        }
        
        [ButtonMethod]
        public void GenerateMesh()
        {
            bool valueChanged = false;
            MeshLevel[] currentLevels = Levels;
            MeshLevel[] levelsCopy = new MeshLevel[currentLevels.Length];
            
            for (int i = 0; i < Levels.Length; i++)
            {
                levelsCopy[i] = new MeshLevel(currentLevels[i]);
            }

            //Field changed
            valueChanged |= previousStartGridSize != startGridSize ||
                            previousMaxSize != maxSize ||
                            previousTop != top ||
                            previousBottom != bottom;

            //Level added or removed
            valueChanged |= previousLevels == null || previousLevels.Length != levelsCopy.Length;

            //Level changed
            if (previousLevels != null)
            {
                for (int i = 0; i < previousLevels.Length && !valueChanged; i++)
                {
                    valueChanged |= !previousLevels[i].Equals(levelsCopy[i]);
                }
            }

            if (valueChanged)
            {
                previousStartGridSize = startGridSize;
                previousMaxSize = maxSize;
                previousLevels = levelsCopy.ToArray();
                previousTop = top;
                previousBottom = bottom;
                
                UpdateLevels();
                bool validMesh = MeshLevelValidation();

                if (!validMesh)
                {
                    return;
                }
                
                Mesh = new Mesh();
                vertices.Clear();
                uv.Clear();
                normals.Clear();
                triangles.Clear();
                triangles2.Clear();
                topMeshVectors.Clear();
                bottomMeshVectors.Clear();
                Mesh.subMeshCount = 2;
                
                currentSize = Levels.Last().Size;
                maxLevelSize = Levels.Last().MaxSize;

                for (var i = 0; i < Levels.Length; i++)
                {
                    MeshLevel topLevel = Levels[^1];
                    MeshLevel innerLevel = i > 0 ? Levels[i - 1] : null;
                    MeshLevel level = Levels[i];
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
                        MeshVectors meshVectors = bottom ? bottomMeshVectors : topMeshVectors;
                        
                        if (meshVectors.AddVector(x, y, vertices.Count))
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
                if (Application.isPlaying)
                {
                    return MeshFilter.mesh;
                }
                else
                {
                    return MeshFilter.sharedMesh;
                }
            }
            private set
            {   
                if (Application.isPlaying)
                {
                    MeshFilter.mesh = value;
                }
                else
                {
                    MeshFilter.sharedMesh = value;
                }
            }
        }
        
        public Material Material
        {
            get
            {   
                if (Application.isPlaying)
                {
                    return Renderer.material;
                }
                else
                {
                    return Renderer.sharedMaterial;
                }
            }
            set
            {
                Material[] materials;
                if (Application.isPlaying)
                {
                    materials = Renderer.materials;
                }
                else
                {
                    materials = Renderer.sharedMaterials;
                }

                if (materials.Length > 0)
                {
                    for (var index = 0; index < materials.Length; index++)
                    {
                        materials[index] = value;
                    }
                    
                    if (Application.isPlaying)
                    {
                        Renderer.materials = materials;
                    }
                    else
                    {
                        Renderer.sharedMaterials = materials;
                    }
                }
                else
                {
                    if (Application.isPlaying)
                    {
                        Renderer.material = value;
                    }
                    else
                    {
                        Renderer.sharedMaterial = value;
                    }
                }
            }
        }

        public float Size => Levels != null && Levels.Length > 0 ? Levels.Last().Size : MaxSize;
        
        public float Offset => Size / 2f;

        public MeshLevel[] Levels
        {
            get
            {
                return usedLevels;
            }
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
        
        public MeshRenderer Renderer
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
        
        public MeshFilter MeshFilter
        {
            get
            {
                if (!meshFilter)
                {
                    meshFilter = GetComponent<MeshFilter>();
                }
        
                return meshFilter;
            }
        }

        public SwellWater Water
        {
            get => water;
            set => water = value;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            MeshVectors meshVectors = top ? topMeshVectors : bottomMeshVectors;
            if (meshVectors.positionToIndex.Count == 0)
            {
                Water?.EditorUpdate();
            }

            if (Selection.transforms.Length > 0 && Selection.transforms.First() == transform && Mesh && Mesh.vertexCount > 0)
            {
                Color gridColor = Color.white;

                if (Material.shader.HasProperty("color"))
                {
                    gridColor = new Color(1 - Material.color.r, 1 - Material.color.g, 1 - Material.color.b);
                }

                Gizmos.color = gridColor;

                DrawFlatGrid();

                DrawMaxSizeBox();
            }
        }

        void DrawFlatGrid()
        {
            for (var i = 0; i < Levels.Length; i++)
            {
                MeshLevel topLevel = Levels[^1];
                MeshLevel innerLevel = i > 0 ? Levels[i - 1] : null;
                MeshLevel level = Levels[i];

                DrawGizmoLevelGrid(level, topLevel, innerLevel);
            }
        }

        void DrawMaxSizeBox()
        {
            if (maxSize > 0)
            {
                Gizmos.color = maxLevelSize > maxSize ? Color.yellow : Color.green;
                Gizmos.DrawWireCube(transform.position, new Vector3(maxSize, 0, maxSize));
            }
        }

        void DrawGizmoLevelGrid(MeshLevel level, MeshLevel topLevel, MeshLevel innerLevel = null)
        {
            float meshSize = level.Size;
            float innerDistance = (innerLevel?.Size) / 2 ?? 0;
            float step = level.Step;
            float offset = level.Offset;

            for (float xi = 0; xi <= meshSize - step; xi += step)
            {
                for (float yi = 0; yi <= meshSize - step; yi += step)
                {
                    float x = xi - offset;
                    float y = yi - offset;


                    if (x < -innerDistance || y < -innerDistance || x >= innerDistance || y >= innerDistance)
                    {
                        Vector2[] pointsInSquare = new Vector2[4];
                        pointsInSquare[0] = new (x, y);
                        pointsInSquare[1] = new (x, y + step);
                        pointsInSquare[2] = new (x + step, y + step);
                        pointsInSquare[3] = new (x + step, y);

                        Vector3[] pointsWithHeight = pointsInSquare.Select(vector =>
                            new Vector3(vector.x, water?.GetHeight(transform.position.x + vector.x, transform.position.z + vector.y) ?? 0, vector.y)).ToArray();
                        
                        DrawPoly(pointsWithHeight, innerDistance);
                    }
                }
            }
        }

        void DrawPoly(Vector3[] points, float innerDistance, bool connect = true)
        {
            for (int i = 0; i < points.Length; i++)
            {
                bool endpoint = i == points.Length - 1;
                bool drawLine = true;
                Vector3 point1 = points[i];
                Vector3 point2 = !endpoint ? points[i + 1] : points[0];

                if (endpoint && !connect)
                {
                    drawLine = false;
                }

                if (point1.x >= -innerDistance && point1.z >= -innerDistance && point1.x <= innerDistance && point1.z <= innerDistance &&  
                    point2.x >= -innerDistance && point2.z >= -innerDistance && point2.x <= innerDistance && point2.z <= innerDistance)
                {
                    drawLine = false;
                }

                if (drawLine)
                {
                    Gizmos.DrawLine(transform.position + point1, transform.position + point2);
                }
            }
        }
#endif
    }
}