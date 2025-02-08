using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TerrainMaterialProcessor
{
    /// 매트리얼 생성
    public static Material CreateMaterial(string savePath, string fileName, Shader shader)
    {
        string materialPath = Path.Combine(savePath, $"{fileName}.mat");
        Material newMaterial = new Material(shader) {
            name = fileName
        };

        AssetDatabase.CreateAsset(newMaterial, materialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Material created at: {materialPath}");
        return newMaterial;
    }

    // 매트리얼에 어레이 넣기
    public static void AssignArrayToMaterial(Material material, string savePath, string baseName, string albedoSuffix, string normalSuffix)
    {
        string albedoArrayPath = $"{savePath}/{baseName}{albedoSuffix}.png";
        Texture2DArray albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(albedoArrayPath);
        if (albedoArray != null)
        {
            material.SetTexture("_ArrayAlbedo", albedoArray);
            Debug.Log($"Albedo array assigned: {albedoArrayPath}");
        }
        else
        {
            Debug.LogWarning($"Albedo array not found at: {albedoArrayPath}");
        }

        string normalArrayPath = $"{savePath}/{baseName}{normalSuffix}.png";
        Texture2DArray normalArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(normalArrayPath);
        if (normalArray != null)
        {
            material.SetTexture("_ArrayNormal", normalArray);
            Debug.Log($"Normal array assigned: {normalArrayPath}");
        }
        else
        {
            Debug.LogWarning($"Normal array not found at: {normalArrayPath}");
        }
    }

    // 매트리얼에 스플랫맵 넣기
    public static void AssignSplatmapsToMaterial(Material material, string materialSavePath, string splatmapBaseName, int splatmapCount)
    {
        if (material == null)
        {
            Debug.LogError("유효하지 않은 매트리얼입니다.");
            return;
        }

        // 스플랫맵 로드
        for (int i = 0; i < splatmapCount; i++)
        {
            string splatmapPath = Path.Combine(materialSavePath, $"{splatmapBaseName}_splatmap{i}.png");

            Texture2D splatmapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(splatmapPath);

            if (splatmapTexture != null)
            {
                string propertyName = $"_T2M_SplatMap_{i}";
                material.SetTexture(propertyName, splatmapTexture);
                Debug.Log($"Assigned {propertyName} with texture: {splatmapPath}");
            }
            else
            {
                Debug.LogWarning($"Splatmap texture not found: {splatmapPath}");
            }
        }

    }

    // 터레인 레이어 기반 매트리얼 설정
    public static void ConfigureMaterialForTerrainLayers(Material material, TerrainLayer[] terrainLayers, Dictionary<string, int> layerIndexMap, float normalStrength)
    {
        if (material == null || terrainLayers == null || terrainLayers.Length == 0)
        {
            Debug.LogError("매트리얼 또는 터레인 레이어가 유효하지 않습니다.");
            return;
        }

        // 텍스처 어레이 인덱스와 노멀 강도 설정
        material.SetFloat("_NormalStrength", normalStrength); // 노멀 세기 설정
        Debug.Log($"Material configured with NormalStrength: {normalStrength}");

        // 레이어별 설정: NormalScale 및 어레이 인덱스
        for (int i = 0; i < 8; i++)
        {
            // 1번부터 시작하는 키워드에 맞게 키 이름 지정
            string normalScaleKey = $"_T2M_Layer_{i}_NormalScale";
            string layerIndexKey = $"_TerrainLayer{i}";

            if (i < terrainLayers.Length)
            {
                TerrainLayer layer = terrainLayers[i];

                // 노말 스케일 값 설정
                float normalScale = layer.normalScale;
                material.SetFloat(normalScaleKey, normalScale);
                Debug.Log($"Set {normalScaleKey} to {normalScale}");

                // 텍스처 어레이 인덱스 매핑
                if (layerIndexMap.TryGetValue(layer.name, out int arrayIndex))
                {
                    material.SetFloat(layerIndexKey, arrayIndex); // 매핑된 어레이 번호 설정
                    Debug.Log($"Set {layerIndexKey} to {arrayIndex} (Layer: {layer.name})");
                }
                else
                {
                    // 어레이에 매핑되지 않은 레이어 처리
                    material.SetFloat(layerIndexKey, -1);
                    Debug.LogWarning($"Layer '{layer.name}' not found in texture array. Set {layerIndexKey} to -1");
                }
            }
            else
            {
                // 레이어가 없는 경우 기본값으로 설정
                material.SetFloat(normalScaleKey, 1.0f);
                material.SetFloat(layerIndexKey, -1); // 없는 레이어는 -1로 설정
                Debug.LogWarning($"Set {normalScaleKey} to default value (1.0f) and {layerIndexKey} to -1");
            }
        }
    }

    // LOD 매트리얼 설정
    public static void ConfigureLodMaterial(Material material, string savePath, string textureName, bool lodNormalTexture, int currentRetry = 0)
    {
        // 최대 재시도 횟수와 대기 간격
        const int maxRetries = 10;      // 최대 재시도 횟수

        if (material == null)
        {
            Debug.LogError("유효하지 않은 매테리얼입니다.");
            return;
        }

        // 텍스처 경로
        string basePath = Path.Combine(savePath, $"{textureName}_AL.png");
        string normalPath = Path.Combine(savePath, $"{textureName}_NO.png");
        string metallicPath = Path.Combine(savePath, $"{textureName}_MS.png");

        bool normalReady = !lodNormalTexture || File.Exists(normalPath);

        // 텍스처 파일이 준비되지 않은 경우 대기
        if (!File.Exists(basePath))
        {
            if (currentRetry >= maxRetries)
            {
                Debug.LogError($"텍스처 생성 실패: {basePath} (최대 재시도 초과)");
                return; // 실패 처리
            }

            Debug.LogWarning($"베이스맵 생성 대기 중... 재시도 ({currentRetry + 1}/{maxRetries})");

            // 재시도 로직 (delayCall 사용)
            EditorApplication.delayCall += () =>
            {
                ConfigureLodMaterial(material, savePath, textureName, normalReady, currentRetry + 1);
            };

            return; // 대기 후 재시도
        }

        // 텍스처 파일 로드
        Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D metallicTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);

        // 매테리얼 설정
        Debug.Log($"텍스처 준비 완료: {basePath}");

        string propertyBaseName = $"_BaseMap";
        material.SetTexture(propertyBaseName, baseTexture);
        material.SetFloat("_Smoothness", 1);

        if (normalTexture != null)
        {
            string propertyNormalName = $"_BumpMap";
            material.SetTexture(propertyNormalName, normalTexture);
        }

        if (metallicTexture)
        {
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
        else
        {
            material.SetFloat("_SmoothnessTextureChannel", 1);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        Debug.Log("LOD 매테리얼 설정 완료!");
    }



    /// 매트리얼의 UV 스케일과 오프셋 설정
    public static void AssignUVScaleAndOffset(Material material, TerrainLayer[] terrainLayers, TerrainData terrainData, int splitcount )
    {
        if (material == null || terrainLayers == null || terrainLayers.Length == 0)
        {
            Debug.LogError("매트리얼 또는 터레인 레이어가 유효하지 않습니다.");
            return;
        }

        Vector3 terrainSize = terrainData.size;

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            // 최대 8개의 레이어만 지원한다고 가정
            if (i >= 8)
            {
                Debug.LogWarning("지원되는 레이어 수를 초과했습니다. 최대 8개까지만 처리됩니다.");
                break;
            }

            TerrainLayer layer = terrainLayers[i];
            if (layer == null) continue;

            // UV 스케일 계산 (정수로 반올림 처리)
            float xScale = Mathf.Round(terrainSize.x / layer.tileSize.x);
            float yScale = Mathf.Round(terrainSize.z / layer.tileSize.y);

            // UV 스케일 및 오프셋 설정
            Vector4 uvScaleOffset = new Vector4(
                xScale / splitcount,                   // X 스케일 (정수)
                yScale / splitcount,                   // Y 스케일 (정수)
                layer.tileOffset.x,       // X 오프셋
                layer.tileOffset.y        // Y 오프셋
            );

            // 쉐이더 프로퍼티에 할당
            string propertyName = $"_T2M_Layer_{i}_uvScaleOffset";
            material.SetVector(propertyName, uvScaleOffset);

            if (Debug.isDebugBuild)
            {
                Debug.Log($"Assigned UV Scale/Offset to {propertyName}: {uvScaleOffset}");
            }
        }
    }


}