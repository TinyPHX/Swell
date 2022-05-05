using System;
using System.Collections.Generic;
// using System.ComponentModel;
using System.Linq;
using System.Text;
using MyBox;
using UnityEngine;
using UnityEngine.PlayerLoop;
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
     * 
     * # Simple Mesh
     * A mesh can be very simple, using only SwellMesh.StartGridSize and SwellMesh.MaxSize to form the grid.
     * 
     * ![SwellMesh (Simple)](https://imgur.com/TW0ShBI.png)
     * 
     * # Advanced Mesh
     * Alternatively you can use levels to define a mesh that gets progressively less coarse as it gets further from
     * it's center.
     * 
     * ![SwellMesh (Advanced)](https://i.imgur.com/oelmhcU.png)
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_mesh.html")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwellMesh : MonoBehaviour
    {
        [field: SerializeField, Min(1)] public int StartGridSize { get; set; } = 1; //!< The Size of the inner most triangles of the mesh. If this is 1, then the shortest distance between any mesh triangle's verts will be 1.
        [field: SerializeField, Min(0)] public int MaxSize { get; set; } = 10; //!< The largest, this mesh can become in world space. When levels are used, this can be set to 0 to allow the levels to grow boundlessly. 
        [field: SerializeField, ReadOnly] public float CurrentSize { get; private set; } = 10; //!< The active size of the mesh. This will not always equal the MaxSize because of partially fitting grid spacing.
        [SerializeField] private MeshLevel[] levels = {};
        public MeshLevel[] Levels { get => usedLevels; set => levels = value; } //!< An alternative to solely using MaxSize. Levels allow a rang of factor increases, creating a grid that gets less dense as it protrudes from it's center.
        [field: SerializeField] public bool Top { get; set; } = true; //!< If true, vectors and triangles are added to the mesh facing upwards. The first MeshRenderer.Material is applied to this mesh. 
        [field: SerializeField] public bool Bottom { get; set; } = true; //!< If true, vectors and triangles are added to the mesh facing downwards. The second MeshRenderer.Material is applied to this mesh.
        [field: SerializeField] public bool LowPolyNormals { get; set; } = false; //!< If true, on generation, vectors are not shared by triangles. This results in a lot of vectors but leaves a cool Low Poly effect that can be used with most Materials. 

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
        private const int MAX_VERT_COUNT = 65535; //https://docs.unity3d.com/ScriptReference/Mesh-indexFormat.html
        
        //Previous state
        private MeshLevel[] previousLevels;
        private float previousStartGridSize;
        private float previousMaxSize;
        private bool previousTop;
        private bool previousBottom;
        private bool previousLowPolyNormals;

        [Serializable]
        public class  MeshLevel
        {
            [HideInInspector] public string name;
            [SerializeField, Min(1)] private int factor;
            [SerializeField, Min(1)] private int count;
            
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

            public bool ValueEquals(object obj)
            {
                if ((obj == null) || GetType() != obj.GetType())
                {
                    return false;
                }
                
                MeshLevel mesh = (MeshLevel)obj;
                return mesh.factor == factor && mesh.count == count && mesh.index == index;
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
                
                if (index == 0 && factor == 0 && count == 0)
                {
                    factor = 1;
                    count = 5;
                    // count = swellMesh.maxSize / swellMesh.startGridSize;
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

            public int Count { get; private set; } = 0;

            public void Clear()
            {
                positionToIndex.Clear();
                xByY.Clear();
                yByX.Clear();

                Count = 0;
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

                    Count++;

                    return true;
                }

                return false;
            }
        }

        void Start()
        {
            GenerateMesh();
        }

        public void EditorUpdate()
        {
            if (Water)
            {
                Water.MeshGridSize = StartGridSize;
                Water.MeshSize = MaxSize;
                if (Top) {Water.RestoreMaterial();} else {Water.ClearMaterial();}
                if (Bottom) {Water.RestoreBottomMaterial();} else {Water.ClearBottomMaterial();}
                Water.LowPolyNormals = LowPolyNormals;
                Water.EditorUpdate();
            }
            else
            {
                GenerateMesh();
                Update();
            }
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
        
        /**
         * @brief When called the mesh vectors, normals, triangles are all recomputed based on the configured properties and levels. After generation the mesh is applied to the MeshFilter.Mesh.
         */
        public void GenerateMesh()
        {
            bool valid = MeshLevelValidation();
            
            bool valueChanged = false;
            MeshLevel[] currentLevels = levels;
            MeshLevel[] levelsCopy = new MeshLevel[currentLevels.Length];
            
            for (int i = 0; i < currentLevels.Length; i++)
            {
                levelsCopy[i] = new MeshLevel(currentLevels[i]);
            }

            //Field changed
            valueChanged |= previousStartGridSize != StartGridSize ||
                            previousMaxSize != MaxSize ||
                            previousTop != Top ||
                            previousBottom != Bottom ||
                            previousLowPolyNormals != LowPolyNormals;

            //Level added or removed
            valueChanged |= previousLevels == null || previousLevels.Length != levelsCopy.Length;

            //Level changed
            if (previousLevels != null)
            {
                for (int i = 0; i < previousLevels.Length && !valueChanged; i++)
                {
                    valueChanged |= !previousLevels[i].ValueEquals(levelsCopy[i]);
                }
            }

            if (valueChanged && valid)
            {
                previousStartGridSize = StartGridSize;
                previousMaxSize = MaxSize;
                previousLevels = levelsCopy.ToArray();
                previousTop = Top;
                previousBottom = Bottom;
                previousLowPolyNormals = LowPolyNormals;
                
                UpdateLevels();

                Mesh mesh = new Mesh();
                
                vertices.Clear();
                uv.Clear();
                normals.Clear();
                triangles.Clear();
                triangles2.Clear();
                topMeshVectors.Clear();
                bottomMeshVectors.Clear();
                mesh.subMeshCount = Convert.ToInt32(Top) + Convert.ToInt32(Bottom);

                int topSubMeshIndex = Top ? 0 : -1;
                int bottomSubMeshIndex = Top && Bottom ? 1 : !Top && Bottom ? 0 : -1;

                if (mesh.subMeshCount > 0)
                {
                    CurrentSize = Levels.Last().Size;
                    maxLevelSize = Levels.Last().MaxSize;

                    for (var i = 0; i < Levels.Length; i++)
                    {
                        MeshLevel topLevel = Levels[^1];
                        MeshLevel innerLevel = i > 0 ? Levels[i - 1] : null;
                        MeshLevel level = Levels[i];
                        if (Top)
                        {
                            AddToMesh(level, topLevel, innerLevel);
                        }

                        if (Bottom)
                        {
                            AddToMesh(level, topLevel, innerLevel, true);
                        }
                    }

                    mesh.vertices = vertices.ToArray();
                    mesh.uv = uv.ToArray();
                    mesh.normals = normals.ToArray();
                    if (topSubMeshIndex > -1)
                    {
                        mesh.SetTriangles(triangles, topSubMeshIndex);
                    }
                    if (bottomSubMeshIndex > -1)
                    {
                        mesh.SetTriangles(triangles2, bottomSubMeshIndex);
                    }

                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();

                    UpdateMeshLowPoly(mesh);

                    mesh.RecalculateNormals();
                }

                Mesh = mesh;
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
            float step = newMeshLevel.Step;
            float offset = newMeshLevel.Offset;
            int normalDirection = bottom ? -1 : 1;

            if (step <= 0)
            {
                Debug.LogWarning("Swell Mesh not updated. Start Size must be > 0");
                return;
            }

            for (float xi = 0; xi <= meshSize && !TooBig; xi += step)
            {
                for (float yi = 0; yi <= meshSize && !TooBig; yi += step)
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

            if (TooBig)
            {
                Debug.LogWarning("Mesh is too big! Unity has a limit of " + MAX_VERT_COUNT + " vectors per mesh and this mesh " +
                                 "has exceeded that limit. To create a larger water you'll have to create and tile " +
                                 "multiple SwellWaters. " +
                                 "https://docs.unity3d.com/ScriptReference/Mesh.SetIndexBufferParams.html");
            }
        }
        
        private void UpdateMeshLowPoly(Mesh mesh)
        {
            if (LowPolyNormals)
            {
                if (mesh.vertices.Length != mesh.triangles.Length)
                {
                    int indexCount = mesh.triangles.Length;
                    if (indexCount > MAX_VERT_COUNT)
                    {
                        Debug.LogWarning("Generating low poly normals requires that many more vectors are added " +
                                         "to our your SwellMesh. Unity has a limit of " + MAX_VERT_COUNT + " vectors per mesh and this " +
                                         "mesh has exceeded that limit. To create a larger low poly water you'll have " +
                                         "to create and tile multiple SwellWaters." +
                                         "https://docs.unity3d.com/ScriptReference/Mesh.SetIndexBufferParams.html");
                    }

                    Vector3[] verts1 = mesh.vertices;
                    Vector2[] uv1 = mesh.uv;
                    int[] tris1 = mesh.triangles;
                    Vector4[] tang1 = mesh.tangents;
                    Vector3[] normal1 = mesh.normals;

                    int newLength = tris1.Length; //Every tri needs dedicated verts not shared with any other tri

                    Vector3[] verts2 = new Vector3[newLength];
                    Vector2[] uv2 = new Vector2[newLength];
                    int[] tris2 = new int[newLength];
                    Vector4[] tang2 = new Vector4[newLength];
                    Vector3[] normal2 = new Vector3[newLength];

                    for (int newIndex = 0; newIndex < newLength; newIndex++)
                    {
                        int oldIndex = tris1[newIndex];
                        tris2[newIndex] = newIndex;
                        verts2[newIndex] = verts1[oldIndex];
                        uv2[newIndex] = uv1[oldIndex];
                        tang2[newIndex] = tang1[oldIndex];
                        normal2[newIndex] = normal1[oldIndex];
                    }

                    mesh.vertices = verts2;
                    mesh.uv = uv2;
                    mesh.tangents = tang2;
                    mesh.normals = normal2;

                    if (mesh.subMeshCount == 1)
                    {
                        mesh.triangles = tris2;
                    }
                    else if (mesh.subMeshCount == 2)
                    {
                        Range mesh1Range = new Range(0, tris2.Length / 2);
                        Range mesh2Range = new Range(tris2.Length / 2, ^0);
                        
                        mesh.SetTriangles(tris2[mesh1Range], 0);
                        mesh.SetTriangles(tris2[mesh2Range], 1);
                    }
                }
            }
        }

        public bool TooBig
        {
            get => bottomMeshVectors.Count + topMeshVectors.Count >= MAX_VERT_COUNT;
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
        } //!< Helper for providing shared assets (sharedMesh) when in edit mode and non-shared assets (mesh) otherwise. 
        
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
        } //Renderer being used by SwellMesh
        
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
        } //MeshFilter being used by SwellMesh.

        public Material[] Materials
        {
            get
            {
                if (Application.isPlaying)
                {
                    return Renderer.materials;
                }
                else
                {
                    return Renderer.sharedMaterials;
                }
            }
            set
            {
                if (Application.isPlaying)
                {
                    Renderer.materials = value;
                }
                else
                {
                    Renderer.sharedMaterials = value;
                }
            }
        } //!< Helper for providing shared assets (sharedMaterials) when in edit mode and non-shared assets (materials) otherwise. 

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
        } //!< Helper for providing shared assets (sharedMaterial) when in edit mode and non-shared assets (material) otherwise. 

        public float Size => Levels != null && Levels.Length > 0 ? Levels.Last().Size : MaxSize;
        
        public float Offset => Size / 2f; //!< Half the size of this mesh.

        public SwellWater Water
        {
            get => water;
            set => water = value;
        } //!< The Water controlling this mesh, if any.

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!TooBig)
            {
                MeshVectors meshVectors = Top ? topMeshVectors : bottomMeshVectors;
                if (meshVectors.positionToIndex.Count == 0)
                {
                    Water?.EditorUpdate();
                }

                if (Selection.transforms.Length > 0 && Selection.transforms.First() == transform && Mesh &&
                    Mesh.vertexCount > 0)
                {
                    Color gridColor = Color.white;

                    if (Material && Material.shader.HasProperty("color"))
                    {
                        gridColor = new Color(1 - Material.color.r, 1 - Material.color.g, 1 - Material.color.b);
                    }

                    Gizmos.color = gridColor;

                    DrawFlatGrid();

                    DrawMaxSizeBox();
                }
            }
        }

        private void DrawFlatGrid()
        {
            for (var i = 0; i < Levels.Length; i++)
            {
                MeshLevel topLevel = Levels[^1];
                MeshLevel innerLevel = i > 0 ? Levels[i - 1] : null;
                MeshLevel level = Levels[i];

                DrawGizmoLevelGrid(level, topLevel, innerLevel);
            }
        }

        private void DrawMaxSizeBox()
        {
            if (MaxSize > 0)
            {
                Gizmos.color = maxLevelSize > MaxSize ? Color.yellow : Color.green;
                Gizmos.DrawWireCube(transform.position, new Vector3(MaxSize, 0, MaxSize));
            }
        }

        private void DrawGizmoLevelGrid(MeshLevel level, MeshLevel topLevel, MeshLevel innerLevel = null)
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

        private void DrawPoly(Vector3[] points, float innerDistance, bool connect = true)
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