using UnityEditor;
using UnityEngine;

public class TerrainHierarchyProcessor
{
    public static GameObject AddObjectToHierarchy(
        string objectName,
        Mesh mesh,
        Material material,
        string parentName = null,
        StaticEditorFlags? staticFlags = null,
        string tag = null,
        int layer = -1,
        bool addMeshCollider = false,
        Vector3? position = null // 위치를 설정할 수 있도록 매개변수 추가
    )
    {
        // 동일 이름의 오브젝트 찾기
        GameObject obj = GameObject.Find(objectName);

        if (obj == null)
        {
            // 오브젝트가 없으면 새로 생성
            obj = new GameObject(objectName);
        }

        // MeshFilter와 MeshRenderer 추가 또는 업데이트
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = obj.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        // 위치 설정
        if (position.HasValue)
        {
            obj.transform.position = position.Value; // 전달받은 위치로 설정
        }

        // 부모 설정
        if (!string.IsNullOrEmpty(parentName))
        {
            GameObject parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName); // 부모가 없으면 생성
            }
            obj.transform.parent = parent.transform; // 부모 설정
        }

        // Static 설정 (초기화 포함)
        if (staticFlags.HasValue)
        {
            GameObjectUtility.SetStaticEditorFlags(obj, staticFlags.Value);
        }
        else
        {
            // Static 체크 해제 시 기본값으로 초기화
            GameObjectUtility.SetStaticEditorFlags(obj, (StaticEditorFlags)0);
        }

        // Tag 설정 (초기화 포함)
        if (!string.IsNullOrEmpty(tag))
        {
            obj.tag = tag;
        }
        else
        {
            // 태그 초기화
            obj.tag = "Untagged";
        }

        // Layer 설정 (초기화 포함)
        if (layer >= 0)
        {
            obj.layer = layer;
        }
        else
        {
            // 레이어 초기화
            obj.layer = 0; // Default 레이어
        }

        // Mesh Collider 추가/제거
        MeshCollider collider = obj.GetComponent<MeshCollider>();
        if (addMeshCollider)
        {
            if (collider == null) collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
        else
        {
            // 콜라이더 제거
            if (collider != null) Object.DestroyImmediate(collider);
        }

        // 하이라키에 추가된 결과 출력
        Debug.Log($"GameObject '{objectName}' processed with settings:\n" +
                  $"Static: {staticFlags}, Tag: {tag}, Layer: {layer}, MeshCollider: {addMeshCollider}, Position: {obj.transform.position}");

        return obj; // 생성된 GameObject 반환
    }

    public static void AddLodObjectToHierarchy(string name, int lodLevel, Mesh lodMesh, Material lodMaterial, Vector3 position)
    {
        AddObjectToHierarchy(name, lodMesh, lodMaterial, position: position);
    }


}