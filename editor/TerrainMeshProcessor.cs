using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class TerrainMeshProcessor
{
    // 메쉬 생성 (기존 코드)
    public static Mesh GenerateMesh(Terrain terrain, int resolution)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain.");
            return null;
        }

        // TerrainData에서 높이 값을 가져와 메쉬 생성
        return GenerateMeshInternal(
            resolution,
            (x, z) => GetInterpolatedHeight(terrain.terrainData, x, z), // 보간된 높이 값 계산
            terrain.terrainData.size
        );
    }

    public static Mesh GenerateMeshFromHeights(float[,] heights, float width, float height, float maxHeight)
    {
        int resolution = heights.GetLength(0) - 1;

        // heights 배열에서 높이 값을 가져와 메쉬 생성
        return GenerateMeshInternal(
            resolution,
            (x, z) => GetInterpolatedHeightFromArray(heights, x, z) * maxHeight,
            new Vector3(width, maxHeight, height)
        );
    }

    // 공통 메쉬 생성 로직
    private static Mesh GenerateMeshInternal(int resolution, System.Func<float, float, float> getHeight, Vector3 size)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        Vector2[] uv = new Vector2[(resolution + 1) * (resolution + 1)];
        int[] triangles = new int[resolution * resolution * 6];

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = x + z * (resolution + 1);

                // 좌표 정규화
                float normalizedX = (float)x / resolution;
                float normalizedZ = (float)z / resolution;

                // 정점 생성
                vertices[index] = new Vector3(
                    normalizedX * size.x,
                    getHeight(normalizedX, normalizedZ), // 높이 계산 함수 호출
                    normalizedZ * size.z
                );

                uv[index] = new Vector2(normalizedX, normalizedZ);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int vertexIndex = x + z * (resolution + 1);

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + resolution + 1;
                triangles[triangleIndex++] = vertexIndex + resolution + 2;

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + resolution + 2;
                triangles[triangleIndex++] = vertexIndex + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    public static (float height, Vector3 normal) GetInterpolatedHeightAndNormal(TerrainData terrainData, float x, float z)
    {
        float height = terrainData.GetHeight(
            Mathf.RoundToInt(x * (terrainData.heightmapResolution - 1)),
            Mathf.RoundToInt(z * (terrainData.heightmapResolution - 1))
        );

        Vector3 normal = terrainData.GetInterpolatedNormal(x, z);
        return (height, normal);
    }


    private static Mesh LodGenerateMeshInternal(int resolution, System.Func<float, float, (float, Vector3)> getHeightAndNormal, Vector3 size, float yOffset, bool edgeDown, float edgeDownDistance)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int baseVertexCount = (resolution + 1) * (resolution + 1);
        int edgeVertexCount = edgeDown ? resolution * 4 + 4 : 0; // 에지 처리에 추가되는 정점 수
        int totalVertexCount = baseVertexCount + edgeVertexCount;

        Vector3[] vertices = new Vector3[totalVertexCount];
        Vector2[] uv = new Vector2[totalVertexCount];
        Vector3[] normals = new Vector3[totalVertexCount];

        int baseTriangleCount = resolution * resolution * 6;
        int edgeTriangleCount = edgeDown ? resolution * 4 * 6 : 0;
        int totalTriangleCount = baseTriangleCount + edgeTriangleCount;

        int[] triangles = new int[totalTriangleCount];

        // 기본 메쉬 생성
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = x + z * (resolution + 1);

                float normalizedX = (float)x / resolution;
                float normalizedZ = (float)z / resolution;

                (float height, Vector3 normal) = getHeightAndNormal(normalizedX, normalizedZ);

                vertices[index] = new Vector3(
                    normalizedX * size.x,
                    height + yOffset,
                    normalizedZ * size.z
                );

                uv[index] = new Vector2(normalizedX, normalizedZ);
                normals[index] = normal;

            }
        }

        // 기본 삼각형 생성
        int triangleIndex = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int vertexIndex = x + z * (resolution + 1);

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + resolution + 1;
                triangles[triangleIndex++] = vertexIndex + resolution + 2;

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + resolution + 2;
                triangles[triangleIndex++] = vertexIndex + 1;
            }
        }

        // 에지 다운 처리
        if (edgeDown)
        {
            int edgeStartIndex = baseVertexCount;

            // 왼쪽 에지 처리
            for (int z = 0; z <= resolution; z++)
            {
                int originalIndex = z * (resolution + 1);
                int newIndex = edgeStartIndex + z;

                vertices[newIndex] = vertices[originalIndex];
                vertices[newIndex].y -= edgeDownDistance;
                uv[newIndex] = uv[originalIndex];
                normals[newIndex] = normals[originalIndex];

                if (z < resolution)
                {
                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = newIndex;
                    triangles[triangleIndex++] = newIndex + 1;

                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = newIndex + 1;
                    triangles[triangleIndex++] = originalIndex + resolution + 1;
                }
            }

            // 아래쪽 에지 처리
            int bottomEdgeStartIndex = edgeStartIndex + (resolution + 1);
            for (int x = 0; x <= resolution; x++)
            {
                int originalIndex = x;
                int newIndex = bottomEdgeStartIndex + x;

                vertices[newIndex] = vertices[originalIndex];
                vertices[newIndex].y -= edgeDownDistance;
                uv[newIndex] = uv[originalIndex];
                normals[newIndex] = normals[originalIndex];

                if (x < resolution)
                {
                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = originalIndex + 1;
                    triangles[triangleIndex++] = newIndex;

                    triangles[triangleIndex++] = originalIndex + 1;
                    triangles[triangleIndex++] = newIndex + 1;
                    triangles[triangleIndex++] = newIndex;
                }
            }

            // 오른쪽 에지 처리
            int rightEdgeStartIndex = bottomEdgeStartIndex + (resolution + 1);
            for (int z = 0; z <= resolution; z++)
            {
                int originalIndex = z * (resolution + 1) + resolution;
                int newIndex = rightEdgeStartIndex + z;

                vertices[newIndex] = vertices[originalIndex];
                vertices[newIndex].y -= edgeDownDistance;
                uv[newIndex] = uv[originalIndex];
                normals[newIndex] = normals[originalIndex];

                if (z < resolution)
                {
                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = newIndex + 1;
                    triangles[triangleIndex++] = newIndex;

                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = originalIndex + resolution + 1;
                    triangles[triangleIndex++] = newIndex + 1;
                }
            }

            // 위쪽 에지 처리
            int topEdgeStartIndex = rightEdgeStartIndex + (resolution + 1);
            for (int x = 0; x <= resolution; x++)
            {
                int originalIndex = resolution * (resolution + 1) + x;
                int newIndex = topEdgeStartIndex + x;

                vertices[newIndex] = vertices[originalIndex];
                vertices[newIndex].y -= edgeDownDistance;
                uv[newIndex] = uv[originalIndex];
                normals[newIndex] = normals[originalIndex];

                if (x < resolution)
                {
                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = newIndex;
                    triangles[triangleIndex++] = newIndex + 1;

                    triangles[triangleIndex++] = originalIndex;
                    triangles[triangleIndex++] = newIndex + 1;
                    triangles[triangleIndex++] = originalIndex + 1;
                }
            }
        }

        // 메쉬 구성
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.normals = normals;
        mesh.triangles = triangles;

        return mesh;
    }



    // 보간을 사용하여 높이를 계산하는 메서드
    public static float GetInterpolatedHeight(TerrainData terrainData, float x, float y)
    {
        // Terrain 데이터의 해상도를 가져옵니다
        int resolution = terrainData.heightmapResolution;

        // 좌표를 0 ~ (resolution-1) 범위로 변환
        float fx = x * (resolution - 1);
        float fy = y * (resolution - 1);

        // 정수 좌표와 잉여값 계산
        int x1 = Mathf.FloorToInt(fx); // 왼쪽 X 좌표
        int x2 = Mathf.Min(x1 + 1, resolution - 1); // 오른쪽 X 좌표
        int y1 = Mathf.FloorToInt(fy); // 아래쪽 Y 좌표
        int y2 = Mathf.Min(y1 + 1, resolution - 1); // 위쪽 Y 좌표

        float tx = fx - x1; // X 보간 비율
        float ty = fy - y1; // Y 보간 비율

        // 주변 4개 지점의 높이를 가져옵니다
        float h1 = terrainData.GetHeight(x1, y1); // 좌하단
        float h2 = terrainData.GetHeight(x2, y1); // 우하단
        float h3 = terrainData.GetHeight(x1, y2); // 좌상단
        float h4 = terrainData.GetHeight(x2, y2); // 우상단

        // X 방향 보간
        float hBottom = Mathf.Lerp(h1, h2, tx); // 하단 보간
        float hTop = Mathf.Lerp(h3, h4, tx);    // 상단 보간

        // Y 방향 보간 (최종 값)
        return Mathf.Lerp(hBottom, hTop, ty);
    }

    public static float GetInterpolatedHeightFromArray(float[,] heights, float x, float y)
    {
        // heights 배열의 해상도
        int resolution = heights.GetLength(0) - 1;

        // 좌표를 0 ~ resolution 범위로 변환
        float fx = x * resolution;
        float fy = y * resolution;

        // 정수 좌표와 잉여값 계산
        int x1 = Mathf.FloorToInt(fx); // 왼쪽 X 좌표
        int x2 = Mathf.Min(x1 + 1, resolution); // 오른쪽 X 좌표
        int y1 = Mathf.FloorToInt(fy); // 아래쪽 Y 좌표
        int y2 = Mathf.Min(y1 + 1, resolution); // 위쪽 Y 좌표

        float tx = fx - x1; // X 보간 비율
        float ty = fy - y1; // Y 보간 비율

        // 주변 4개 지점의 높이를 가져옵니다
        float h1 = heights[y1, x1]; // 좌하단
        float h2 = heights[y1, x2]; // 우하단
        float h3 = heights[y2, x1]; // 좌상단
        float h4 = heights[y2, x2]; // 우상단

        // X 방향 보간
        float hBottom = Mathf.Lerp(h1, h2, tx); // 하단 보간
        float hTop = Mathf.Lerp(h3, h4, tx);    // 상단 보간

        // Y 방향 보간 (최종 값)
        return Mathf.Lerp(hBottom, hTop, ty);
    }



    public static Mesh AdjustUVForSplitMesh(Mesh mesh, int splitIndexX, int splitIndexZ, int splitCount, float terrainWidth, float terrainHeight)
    {
        Vector2[] uvs = mesh.uv;

        // 분할된 조각의 UV 스케일 및 오프셋 계산
        float uvScaleX = 1.0f / splitCount; // X축 스케일 (조각 크기 비율)
        float uvScaleZ = 1.0f / splitCount; // Z축 스케일 (조각 크기 비율)
        float uvOffsetX = splitIndexX * uvScaleX; // X축 오프셋 (조각의 시작 위치)
        float uvOffsetZ = splitIndexZ * uvScaleZ; // Z축 오프셋 (조각의 시작 위치)

        for (int i = 0; i < uvs.Length; i++)
        {
            // 원래 UV 좌표를 분할 영역에 맞게 조정
            uvs[i] = new Vector2(
                uvOffsetX + uvs[i].x * uvScaleX, // X축 조정
                uvOffsetZ + uvs[i].y * uvScaleZ  // Z축 조정
            );
        }

        // 조정된 UV 적용
        mesh.uv = uvs;

        return mesh;
    }

    public static Mesh GenerateLodMesh(Terrain terrain, int lodLevel, int baseResolution, float yOffset, bool edgeDown, float edgeDownDistance)
    {
        int lodResolution = (int)Mathf.Max(1, baseResolution / Mathf.Pow(2, lodLevel));
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain.");
            return null;
        }

        return LodGenerateMeshInternal(
            lodResolution,
            (x, z) => GetInterpolatedHeightAndNormal(terrain.terrainData, x, z), // 높이 + 노멀 가져오기
            terrain.terrainData.size,
            yOffset,
            edgeDown,
            edgeDownDistance
        );
    }



    public static Mesh GenerateLodSplitMesh(
        float[,] heights,
        TerrainData terrainData,
        float width,
        float height,
        float maxHeight,
        int lodLevel,
        float yOffset,
        bool edgeDown,
        float edgeDownDistance,
        Rect uvBounds // UV 범위
    )
    {
        int resolution = (int)((heights.GetLength(0) - 1) / Mathf.Pow(2, lodLevel));

        return LodGenerateMeshInternal(
            resolution,
            (x, z) =>
            {
            // UV 좌표를 기반으로 월드 좌표 계산
            float worldX = uvBounds.xMin + x * uvBounds.width;
                float worldZ = uvBounds.yMin + z * uvBounds.height;

            // Terrain 높이와 노말 가져오기
            float interpolatedHeight = GetInterpolatedHeightFromArray(heights, x, z) * maxHeight;
                Vector3 interpolatedNormal = terrainData.GetInterpolatedNormal(worldX, worldZ);

                return (interpolatedHeight, interpolatedNormal);
            },
            new Vector3(width, maxHeight, height),
            yOffset,
            edgeDown,
            edgeDownDistance
        );
    }


    // .obj 형식으로 저장
    public static void SaveMeshAsOBJ(Mesh mesh, string savePath, string fileName)
    {
        // .obj 파일 경로 생성
        string objPath = Path.Combine(savePath, fileName + ".obj");

        StringBuilder sb = new StringBuilder();

        // Object 이름 설정
        sb.AppendLine($"o {fileName}");
        sb.AppendLine($"g {fileName}");

        // Vertex 저장
        foreach (Vector3 vertex in mesh.vertices)
        {
            sb.AppendLine($"v {-vertex.x} {vertex.y} {vertex.z}");
        }

        // UV 저장
        foreach (Vector2 uv in mesh.uv)
        {
            sb.AppendLine($"vt {uv.x} {uv.y}");
        }

        // Normal 저장
        foreach (Vector3 normal in mesh.normals)
        {
            sb.AppendLine($"vn {-normal.x} {normal.y} {normal.z}");
        }

        // Face 저장
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i] + 1;
            int v2 = triangles[i + 1] + 1;
            int v3 = triangles[i + 2] + 1;

            sb.AppendLine($"f {v1}/{v1}/{v1} {v3}/{v3}/{v3} {v2}/{v2}/{v2}");
        }

        // 파일 쓰기
        File.WriteAllText(objPath, sb.ToString());
        Debug.Log($"Mesh saved as OBJ at: {objPath}");

        // 저장한 .obj 파일의 경로를 Unity 경로로 변환
        string assetPath = objPath.Replace(Application.dataPath, "Assets").Replace("\\", "/");

        // AssetDatabase 갱신 후 ModelImporter 설정 조작
        AssetDatabase.Refresh();

        // 일정 시간 지연 후 ModelImporter 설정
        EditorApplication.delayCall += () =>
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                // 필요한 설정 비활성화
                modelImporter.animationType = ModelImporterAnimationType.None; // Rig 비활성화
                modelImporter.importAnimation = false;                         // 애니메이션 비활성화
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None; // 머티리얼 임포트 비활성화

                // 변경사항 저장 및 재임포트
                modelImporter.SaveAndReimport();
                Debug.Log($"Import settings updated for: {assetPath}");
            }
            else
            {
                Debug.LogError($"Failed to find ModelImporter for: {assetPath}");
            }
        };
    }



    // Unity .asset 형식으로 저장
    // png 사용하고 있어서 안쓰고 있음
    public static void SaveMeshAsAsset(Mesh mesh, string savePath, string fileName)
    {
        string path = Path.Combine(savePath, fileName + ".asset");

        // 경로가 유효하지 않으면 중단
        if (!path.StartsWith(Application.dataPath))
        {
            Debug.LogError("Invalid path. The path must be within the Unity project.");
            return;
        }

        string assetPath = "Assets" + path.Substring(Application.dataPath.Length);

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Mesh saved as Unity Asset at: {assetPath}");
        UnityEditor.AssetDatabase.Refresh();
    }


}
