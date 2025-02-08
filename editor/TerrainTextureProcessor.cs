using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;
using System.Linq;

public class TerrainTextureProcessor
{
    public Terrain[] terrains;
    public List<Texture2D> albedoTextures = new List<Texture2D>();
    public List<Texture2D> normalTextures = new List<Texture2D>();

    // Texture Array 옵션
    public int textureSize = 512;
    public TextureFormat textureFormat = TextureFormat.RGBA32;
    public bool useMipMap = true;
    public FilterMode filterMode = FilterMode.Bilinear;

    // 스플랫맵 관련 옵션 추가
    public int splatmapSize = -1; // Default: -1 (Terrain 설정에 따라 결정)

    public List<(string name, Texture2D albedo, Texture2D normal)> layerTextures = new List<(string, Texture2D, Texture2D)>();

    public enum TextureType
    {
        Albedo,
        Smoothness,
        Normal
    }


    // 고유 텍스처 수집
    public void CollectUniqueTexturesByLayer()
    {
        layerTextures.Clear();

        foreach (var terrain in terrains)
        {
            if (terrain == null) continue;

            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            foreach (var layer in layers)
            {
                if (layer == null) continue;

                // 중복 체크
                if (!layerTextures.Exists(l => l.name == layer.name))
                {
                    layerTextures.Add((layer.name, layer.diffuseTexture, layer.normalMapTexture));
                }
            }
        }

        Debug.Log($"레이어 수집 완료 - 총 레이어: {layerTextures.Count}");
    }


    // Texture Atlas 생성
    public void GenerateTextureAtlas(string baseName, string savePath, string albedoSuffix, string normalSuffix)
    {
        if (layerTextures.Count > 0)
        {
            // 알베도 텍스처 아틀라스 생성
            var albedoList = layerTextures.Select(layer => layer.albedo).ToList();
            CreateAtlas(albedoList, savePath, $"{baseName}{albedoSuffix}", isNormalMap: false);

            // 노말 텍스처 아틀라스 생성
            var normalList = layerTextures.Select(layer => layer.normal).ToList();
            CreateAtlas(normalList, savePath, $"{baseName}{normalSuffix}", isNormalMap: true);
        }
        else
        {
            Debug.LogWarning("No textures available for atlas generation.");
        }
    }


    // 스플랫맵 텍스처 생성
    public void GenerateSingleSplatmap(string savePath , string filePrefix)
    {
        if (terrains == null || terrains.Length == 0)
        {
            Debug.LogWarning("No terrains available for splatmap generation.");
            return;
        }

        // Terrain별로 처리
        for (int t = 0; t < terrains.Length; t++)
        {
            Terrain terrain = terrains[t];
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning($"Skipping invalid terrain at index {t}.");
                continue;
            }

            // 스플랫맵 해상도 결정
            int fullResolution = terrain.terrainData.alphamapResolution;

            // 사용자 지정 크기 또는 디폴트
            int resolution = splatmapSize == -1
                ? fullResolution
                : Mathf.Min(splatmapSize, fullResolution); // 해상도 제한

            float[,,] alphaMaps = terrain.terrainData.GetAlphamaps(0, 0, resolution, resolution);
            int layerCount = terrain.terrainData.alphamapLayers;

            Debug.Log($"Generating splatmaps for Terrain '{terrain.name}' with resolution {resolution} and {layerCount} layers.");

            // 필요한 Splatmap 개수 계산 (4개 채널씩 병합)
            int splatmapCount = Mathf.CeilToInt((float)layerCount / 4);

            for (int i = 0; i < splatmapCount; i++)
            {
                // RGBA 병합 (4채널씩 병합)
                Texture2D mergedSplatmap = MergeSplatmapLayersToRGBA(alphaMaps, resolution, layerCount, i * 4);

                // 저장 경로 지정 (filePrefix 적용)
                string splatmapPath = Path.Combine(savePath, $"{filePrefix}{terrain.name}_splatmap{i}.png");
                SaveSplatmapAsPNG(mergedSplatmap, splatmapPath);

                Debug.Log($"Splatmap for Terrain '{terrain.name}', Part {i} saved at: {splatmapPath}");

                // 메모리 정리
                Object.DestroyImmediate(mergedSplatmap);
            }
        }

        // 에셋 데이터 갱신
        Debug.Log("Splatmap generation completed.");
    }

    public Dictionary<string, int> CreateLayerIndexMap()
    {
        var layerIndexMap = new Dictionary<string, int>();

        for (int i = 0; i < layerTextures.Count; i++)
        {
            var layer = layerTextures[i];

            // 튜플의 요소를 개별적으로 확인
            if (!string.IsNullOrEmpty(layer.name) && layer.albedo != null && !layerIndexMap.ContainsKey(layer.name))
            {
                layerIndexMap.Add(layer.name, i);
            }
        }

        return layerIndexMap;
    }

    public void GenerateSplitSplatmaps(Terrain terrain, int splitCount, string savePath, string filePrefix)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain for splatmap generation.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;

        // 전체 스플랫맵 해상도 및 레이어 개수
        int fullResolution = terrainData.alphamapResolution;
        int layerCount = terrainData.alphamapLayers;

        // 사용자 지정 크기 또는 디폴트
        int targetResolution = splatmapSize == -1
            ? fullResolution / splitCount
            : Mathf.Min(splatmapSize / splitCount, fullResolution/ splitCount); // 해상도 제한

        // 최소 해상도 확인
        if (targetResolution < 2)
        {
            Debug.LogError($"Splatmap resolution too small! Resolution per chunk ({targetResolution}x{targetResolution}) is invalid. Please increase the splatmap size or reduce the split count.");
            return;
        }

        // 각 조각 처리
        for (int z = 0; z < splitCount; z++)
        {
            for (int x = 0; x < splitCount; x++)
            {
                // 조각별 스플랫맵 데이터 추출
                float[,,] alphaMaps = terrainData.GetAlphamaps(
                    x * fullResolution / splitCount,
                    z * fullResolution / splitCount,
                    targetResolution,
                    targetResolution
                );

                // 필요한 Splatmap 개수 계산 (4개 채널씩 병합)
                int splatmapCount = Mathf.CeilToInt((float)layerCount / 4);

                for (int i = 0; i < splatmapCount; i++)
                {
                    // RGBA 병합 (4채널씩 병합)
                    Texture2D mergedSplatmap = MergeSplatmapLayersToRGBA(alphaMaps, targetResolution, layerCount, i * 4);

                    // 저장 경로 지정 (파일명에 조각 정보 추가)
                    string splatmapPath = Path.Combine(savePath, $"{filePrefix}{terrain.name}_{x}_{z}_splatmap{i}.png");
                    SaveSplatmapAsPNG(mergedSplatmap, splatmapPath);

                    Debug.Log($"Splatmap for Chunk ({x}, {z}), Part {i} saved at: {splatmapPath}");

                    // 메모리 정리
                    Object.DestroyImmediate(mergedSplatmap);
                }
            }
        }

        Debug.Log("Split Splatmap generation completed.");
    }


    private Texture2D MergeSplatmapLayersToRGBA(float[,,] splatmapData, int resolution, int layerCount, int startLayer)
    {
        // RGBA 텍스처 생성
        Texture2D splatTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

        // RGBA 채널 초기화
        Color[] pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // 픽셀 인덱스
                int index = x + y * resolution;

                // R, G, B, A 값 채우기 (해당 채널이 없으면 0)
                float r = (startLayer + 0) < layerCount ? splatmapData[y, x, startLayer + 0] : 0f;
                float g = (startLayer + 1) < layerCount ? splatmapData[y, x, startLayer + 1] : 0f;
                float b = (startLayer + 2) < layerCount ? splatmapData[y, x, startLayer + 2] : 0f;
                float a = (startLayer + 3) < layerCount ? splatmapData[y, x, startLayer + 3] : 1f;

                pixels[index] = new Color(r, g, b, a);
            }
        }

        // 픽셀 데이터 적용
        splatTexture.SetPixels(pixels);
        splatTexture.Apply();

        return splatTexture;
    }


    public void LodRenderToTexture(Terrain terrain, Material material)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain for texture rendering.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        TerrainLayer[] layers = terrainData.terrainLayers;
        Vector3 terrainSize = terrainData.size;

        if (layers == null || layers.Length == 0)
        {
            Debug.LogError("No terrain layers found!");
            return;
        }

        
        // 스플랫맵 데이터 가져오기
        float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapResolution, terrainData.alphamapResolution);

        // 스플랫맵 데이터 생성
        Texture2D splatmap1 = ConvertAlphamapToTexture(alphaMaps, 0); // 첫 번째 스플랫맵 (레이어 0~3)
        Texture2D splatmap2 = ConvertAlphamapToTexture(alphaMaps, 1); // 두 번째 스플랫맵 (레이어 4~7)

        // Material에 스플랫맵 연결
        material.SetTexture("_Splatmap1", splatmap1); // 첫 번째 스플랫맵
        material.SetTexture("_Splatmap2", splatmap2); // 두 번째 스플랫맵

        // 각 레이어의 텍스처 설정
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].diffuseTexture != null)
            {
                material.SetTexture($"_Layer{i}_BaseMap", layers[i].diffuseTexture);
            }

            if (layers[i].normalMapTexture != null)
            {
                material.SetTexture($"_Layer{i}_NormalMap", layers[i].normalMapTexture);
            }

            float xScale = Mathf.Round(terrainSize.x / layers[i].tileSize.x);
            float yScale = Mathf.Round(terrainSize.z / layers[i].tileSize.y);
            Vector4 uvScale = new Vector4(xScale, yScale, 0, 0);
            material.SetVector($"_Layer{i}_UVScale", uvScale);

            material.SetFloat($"_Layer{i}_NormalStrength", layers[i].normalScale);
        }

        Debug.Log("Material configured for LOD texture rendering.");
        return; // 렌더링에 사용할 Material 반환
    }


    public void GenerateLodTexture(Terrain terrain, int resolution, string savePath, string lodPrefix, bool normalToggle)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain for unified texture generation.");
            return;
        }

        Material customMaterial = new Material(Shader.Find("Hidden/Custom/TerrainUnifiedShader"));

        // 렌더링용 Material 생성
        LodRenderToTexture(terrain, customMaterial);
        if (customMaterial == null)
        {
            Debug.LogError("Failed to generate material for LOD texture rendering.");
            return;
        }

        // BaseMap 촬영 및 저장
        GenerateTexture(customMaterial, resolution, savePath, $"{lodPrefix}{terrain.name}_AL", isNormalMap: false, TextureType.Albedo);
        if (normalToggle)
        {
            GenerateTexture(customMaterial, resolution, savePath, $"{lodPrefix}{terrain.name}_NO", isNormalMap: true, TextureType.Normal);
        }

        Debug.Log($"LOD Texture generation completed for: {terrain.name}");
    }


    public void GenerateLodSplitTextures(Terrain terrain, int splitCount, int resolution, string savePath, string lodPrefix, bool includeNormalMap)
    {
        // LOD 렌더링용 Material 설정
        Material customMaterial = new Material(Shader.Find("Hidden/Custom/TerrainUnifiedShader"));
        LodRenderToTexture(terrain, customMaterial);

        // RenderTexture 생성
        RenderTexture rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        rt.enableRandomWrite = true;
        rt.Create();

        // 알베도 텍스처 촬영
        customMaterial.EnableKeyword("_TEXTURE_TYPE_ALBEDO");
        customMaterial.DisableKeyword("_TEXTURE_TYPE_NORMAL");
        Graphics.Blit(null, rt, customMaterial, 0);

        // 알베도 RenderTexture -> Texture2D 변환
        Texture2D fullAlbedoTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        fullAlbedoTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        fullAlbedoTexture.Apply();

        // 노말 맵 텍스처 촬영 (선택적 처리)
        Texture2D fullNormalTexture = null;
        if (includeNormalMap)
        {
            customMaterial.DisableKeyword("_TEXTURE_TYPE_ALBEDO");
            customMaterial.EnableKeyword("_TEXTURE_TYPE_NORMAL");
            Graphics.Blit(null, rt, customMaterial, 0);

            fullNormalTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            fullNormalTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            fullNormalTexture.Apply();
        }

        RenderTexture.active = null;
        rt.Release();

        // 텍스처 조각 분리 및 저장
        int chunkResolution = resolution / splitCount;
        for (int z = 0; z < splitCount; z++)
        {
            for (int x = 0; x < splitCount; x++)
            {
                // 알베도 조각 추출 및 저장
                Texture2D albedoChunk = ExtractChunkTexture(fullAlbedoTexture, chunkResolution, x, z);
                string albedoPath = Path.Combine(savePath, $"{lodPrefix}{terrain.name}_{x}_{z}_AL.png");
                SaveTextureAsPNG(albedoChunk, savePath, albedoPath);
                LodConfigureTexture(albedoPath, isNormalMap: false);
                Debug.Log($"Saved Albedo Chunk: {albedoPath}");
                Object.DestroyImmediate(albedoChunk); // 메모리 정리

                // 노말 맵 조각 추출 및 저장
                if (includeNormalMap && fullNormalTexture != null)
                {
                    Texture2D normalChunk = ExtractChunkTexture(fullNormalTexture, chunkResolution, x, z);
                    string normalPath = Path.Combine(savePath, $"{lodPrefix}{terrain.name}_{x}_{z}_NO.png");
                    SaveTextureAsPNG(normalChunk, savePath, normalPath);
                    LodConfigureTexture(normalPath, isNormalMap: true);
                    Debug.Log($"Saved Normal Chunk: {normalPath}");
                    Object.DestroyImmediate(normalChunk); // 메모리 정리
                }
            }
        }

        Object.DestroyImmediate(fullAlbedoTexture); // 메모리 정리
        if (fullNormalTexture != null)
            Object.DestroyImmediate(fullNormalTexture);
    }

    private Texture2D ExtractChunkTexture(Texture2D fullTexture, int chunkResolution, int chunkX, int chunkZ)
    {
        Texture2D chunkTexture = new Texture2D(chunkResolution, chunkResolution, TextureFormat.RGBA32, false);
        Color[] chunkPixels = fullTexture.GetPixels(
            chunkX * chunkResolution, // 시작 X 좌표
            chunkZ * chunkResolution, // 시작 Z 좌표
            chunkResolution,          // 가로 크기
            chunkResolution           // 세로 크기
        );
        chunkTexture.SetPixels(chunkPixels);
        chunkTexture.Apply();
        return chunkTexture;
    }




    // 스플랫맵 데이터를 텍스처로 변환
    private static Texture2D ConvertAlphamapToTexture(float[,,] alphaMaps, int mapIndex)
    {
        int resolution = alphaMaps.GetLength(0);
        int layerCount = alphaMaps.GetLength(2);
        int startLayer = mapIndex * 4;

        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = x + y * resolution;

                // R, G, B, A 값을 병합 (리니어 데이터 그대로 사용)
                float r = (startLayer + 0) < layerCount ? Mathf.Clamp01(alphaMaps[y, x, startLayer + 0]) : 0f;
                float g = (startLayer + 1) < layerCount ? Mathf.Clamp01(alphaMaps[y, x, startLayer + 1]) : 0f;
                float b = (startLayer + 2) < layerCount ? Mathf.Clamp01(alphaMaps[y, x, startLayer + 2]) : 0f;
                float a = (startLayer + 3) < layerCount ? Mathf.Clamp01(alphaMaps[y, x, startLayer + 3]) : 1f;

                pixels[index] = new Color(r, g, b, a);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();


        return texture;
    }


    private static void GenerateTexture(Material material, int resolution, string savePath, string fileName, bool isNormalMap, TextureType textureType)
    {
        // 키워드 설정을 Enum 기반으로 관리
        Dictionary<TextureType, string> textureKeywords = new Dictionary<TextureType, string>
        {
        { TextureType.Albedo, "_TEXTURE_TYPE_ALBEDO" },
        { TextureType.Normal, "_TEXTURE_TYPE_NORMAL" }
    };

        // RenderTexture 생성 (색공간 설정 가능)
        RenderTexture rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        rt.enableRandomWrite = true;
        rt.Create();

        // 키워드 설정 (모든 키워드를 비활성화 후, 필요한 키워드만 활성화)
        foreach (var keyword in textureKeywords.Values)
            material.DisableKeyword(keyword);
        material.EnableKeyword(textureKeywords[textureType]);


        Graphics.Blit(null, rt, material, 0);

        // AsyncGPUReadback 요청 (비동기 저장)
        AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, request =>
        {
            if (request.hasError)
            {
                Debug.LogError($"Failed to generate texture: {fileName}");
                return;
            }

            // Texture2D 변환 및 저장
            var data = request.GetData<Color32>();
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.SetPixelData(data.ToArray(), 0);
            texture.Apply();

            string fullPath = Path.Combine(savePath, $"{fileName}.png");
            SaveTextureAsPNG(texture, savePath, fullPath);

            // LodConfigureTexture 복구 (Import 설정)
            LodConfigureTexture(fullPath, isNormalMap);

            // 정리
            RenderTexture.active = null;
            rt.Release();
            Object.DestroyImmediate(texture);

            Debug.Log($"Texture saved: {fullPath}");
        });

        // 정리
        RenderTexture.active = null;
        rt.Release();
    }



    private static void LodConfigureTexture(string path, bool isNormalMap)
    {
        // TextureImporter 가져오기
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Failed to configure texture at: {path}");
            return;
        }

        // TextureImporterSettings 가져오기
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        // 설정 수정
        settings.textureType = isNormalMap ? UnityEditor.TextureImporterType.NormalMap : UnityEditor.TextureImporterType.Default;
        settings.wrapMode = TextureWrapMode.Clamp;

        // 수정된 설정 다시 적용
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

    }


    private void ConfigureTexture(string path, bool isNormalMap, int columns, int rows)
    {
        // TextureImporter 가져오기
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Failed to configure texture at: {path}");
            return;
        }

        // TextureImporterSettings 가져오기
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        // 설정 수정
        settings.textureType = isNormalMap ? UnityEditor.TextureImporterType.NormalMap : UnityEditor.TextureImporterType.Default;
        settings.textureShape = UnityEditor.TextureImporterShape.Texture2DArray;
        settings.flipbookColumns = columns; // Columns 설정
        settings.flipbookRows = rows;       // Rows 설정

        // 수정된 설정 다시 적용
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        Debug.Log($"Configured texture at {path} as {(isNormalMap ? "Normal Map" : "Default")} with 2D Array. Columns: {columns}, Rows: {rows}");
    }



    private (int columns, int rows) CalculateGridSize(int textureCount)
    {
        // 기본적으로 가로(열)는 최대 4로 제한
        int columns = Mathf.Min(4, textureCount);
        int rows = Mathf.CeilToInt((float)textureCount / columns); // 행은 필요한 만큼 계산

        return (columns, rows);
    }



    private Texture2D CreateEmptyTexture()
    {
        Texture2D empty = new Texture2D(textureSize, textureSize, textureFormat, useMipMap);
        empty.filterMode = filterMode;
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear; // 투명한 텍스처
        empty.SetPixels(pixels);
        empty.Apply();
        return empty;
    }

    private void CreateAtlas(List<Texture2D> textures, string savePath, string atlasName, bool isNormalMap = false)
    {
        if (textures.Count == 0) return;

        // 열과 행 계산
        (int columns, int rows) = CalculateGridSize(textures.Count);

        // 아틀라스 크기 고정
        int atlasWidth = columns * textureSize;
        int atlasHeight = rows * textureSize;

        // 아틀라스 생성
        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, textureFormat, useMipMap);
        atlas.filterMode = filterMode;

        // 텍스처를 아틀라스에 배치
        for (int i = 0; i < textures.Count; i++)
        {
            Texture2D source = textures[i] ?? CreateEmptyTexture(); // 빈 텍스처 대체
            Texture2D resizedTexture = ResizeUsingRenderTexture(source, isNormalMap);

            int xPosition = (i % columns) * textureSize; // 열 위치
            int yPosition = (rows - 1 - (i / columns)) * textureSize; // 행 위치 (위에서부터 아래로)

            atlas.SetPixels(xPosition, yPosition, textureSize, textureSize, resizedTexture.GetPixels());
            Object.DestroyImmediate(resizedTexture);
        }

        atlas.Apply();

        string savePathWithName = $"{savePath}/{atlasName}.png";
        SaveTextureAsPNG(atlas, savePath, savePathWithName);

        ConfigureTexture(savePathWithName, isNormalMap, columns, rows);

        Debug.Log($"Atlas 생성 완료: {savePathWithName} (크기: {atlasWidth}x{atlasHeight}, 텍스처 개수: {textures.Count})");
    }



    private Texture2D ResizeUsingRenderTexture(Texture2D source, bool isNormalMap = false)
    {
        // 색 공간 결정
        RenderTextureReadWrite readWrite = isNormalMap ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;

        // RenderTexture 생성
        RenderTexture rt = RenderTexture.GetTemporary(textureSize, textureSize, 0, RenderTextureFormat.ARGB32, readWrite);
        rt.filterMode = filterMode;

        // Blit 및 텍스처 생성
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, useMipMap);
        result.filterMode = filterMode;
        result.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // 노말 맵 보정
        if (isNormalMap)
        {
            Color[] pixels = result.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float x = pixels[i].a * 2 - 1;
                float y = pixels[i].r * 2 - 1;
                float z = Mathf.Sqrt(1 - Mathf.Clamp01(x * x + y * y));
                pixels[i] = new Color(x * 0.5f + 0.5f, y * 0.5f + 0.5f, z * 0.5f + 0.5f, 1.0f);
            }
            result.SetPixels(pixels);
            result.Apply();
        }

        return result;
    }

    private void SaveSplatmapAsPNG(Texture2D texture, string path)
    {
        // 저장 디렉토리 확인 및 생성
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // PNG로 저장
        byte[] pngData = texture.EncodeToPNG();
        if (pngData != null)
        {
            File.WriteAllBytes(path, pngData);
            Debug.Log($"Splatmap saved at: {path}");
        }

        // 에셋 데이터 갱신
        AssetDatabase.Refresh();

        // TextureImporter 설정 변경
        string assetPath = path.Replace(Application.dataPath, "Assets");
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            // 설정 조정
            importer.sRGBTexture = false; // sRGB 비활성화
            importer.isReadable = false; // Read/Write 비활성화
            importer.wrapMode = TextureWrapMode.Clamp; // Wrap Mode = Clamp
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 압축 없음
            importer.SaveAndReimport();

            Debug.Log($"Updated TextureImporter settings for: {assetPath}");
        }
    }


    private static void SaveTextureAsPNG(Texture2D texture, string savePath, string fullpath)
    {
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        byte[] pngData = texture.EncodeToPNG();
        if (pngData != null)
        {
            File.WriteAllBytes(fullpath, pngData);
        }

        // 에셋 데이터베이스 갱신
        UnityEditor.AssetDatabase.Refresh();
    }
}
