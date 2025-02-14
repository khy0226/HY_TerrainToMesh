using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class HY_TerrainToMesh : EditorWindow
{
    private enum Tab { TextureConversion, MeshConversion, LODCreate }
    private Tab currentTab = Tab.TextureConversion;

    private TerrainTextureProcessor textureProcessor = new TerrainTextureProcessor();
    private ReorderableList terrainList;

    private Vector2 mainScrollPosition = Vector2.zero;

    private int selectedResolution = -1; // 기본 해상도 (-1이면 자동 설정)
    private Vector2 scrollPos; // 테이블 스크롤 위치 저장
    private Vector2 textureScrollPos; // 텍스쳐 스크롤 위치 저장

    private bool isMeshResolutionSet = false; // Mesh Resolution 설정 여부 플래그

    private string filePrefix = "terrain_"; // 기본 접두사
    private Shader selectedShader; // 선택한 쉐이더 지형용
    private string defaultShaderName = "Shader Graphs/TerrainMeshSplatmap"; // 기본 쉐이더 이름
    private Shader selectedLitShader; // 선택한 쉐이더 lod용

    public int splatmapSize = -1; // Default: -1 (Terrain 설정에 따라 결정)

    public string savePath = "Assets/Generated";
    //텍스처 변환에 있는 저장옵션
    public string baseName = "TerrainTextureArray";
    public string albedoSuffix = "_Albedo";
    public string normalSuffix = "_Normal";

    private bool addToHierarchy = false; // 하이라키에 추가 여부를 저장
    private GameObject parentObject; // 하이라키에 미리 만든 부모가 있을경우
    private string parentHierarchyName = ""; // 부모 이름

    private bool useStatic = false;
    private bool useTag = false;
    private bool useLayer = false;
    private bool addMeshCollider = true; // 기본 활성화

    // Static, Tag, Layer 값을 저장
    private StaticEditorFlags staticFlags = 0;
    private string selectedTag = "Untagged";
    private int selectedLayer = 0;

    private bool splitOption = false;
    private int selectedSplitCount = 2;
    private bool sameMaterial = true;

    private string lodPrefix = "LOD1_";
    private int terrainLod = 3;
    private bool lodTexture = true;
    private bool lodNormalTexture = true;
    private int lodTextureSize = 128;

    private bool lodMeshSplit = false;
    private bool lodTextureSplit = true;

    private float yOffset = 0;
    private bool edgeDown = true;
    private float edgeDownDistance = 20;
    private GameObject lodParentObject; 
    private string lodParentHierarchyName = "";

    private bool lodUseStatic = false;
    private bool lodUseTag = false;
    private bool lodUseLayer = false;
    private bool lodAddMeshCollider = false; // 기본 활성화
    private StaticEditorFlags lodStaticFlags = 0;
    private string lodSelectedTag = "Untagged";
    private int lodSelectedLayer = 0;

    [MenuItem("HY/터레인을 메쉬로 변환툴")]
    public static void ShowWindow()
    {
        // 창 생성
        HY_TerrainToMesh window = GetWindow<HY_TerrainToMesh>("Terrain To Mesh Conversion");

        // 초기 크기 설정
        window.position = new Rect(100, 100, 600, 700); // 초기 위치와 크기 지정
        window.minSize = new Vector2(200, 200);         // 최소 크기 설정
    }

    private void OnEnable()
    {
        if (textureProcessor.terrains == null)
        {
            textureProcessor.terrains = new Terrain[0];
        }

        InitializeTerrainList();

        // 첫 번째 항목 선택
        if (textureProcessor.terrains.Length > 0)
        {
            terrainList.index = 0; // 첫 번째 항목 자동 선택
            UpdateMeshResolutionBasedOnTerrain(textureProcessor.terrains[0]);
        }

        // 특정 이름의 쉐이더를 찾아 기본값으로 설정
        selectedShader = Shader.Find(defaultShaderName);

        if (selectedShader == null)
        {
            Debug.LogWarning($"Shader '{defaultShaderName}' not found. Falling back to Standard shader.");
            selectedShader = Shader.Find("Shader Graphs/TerrainMeshSplatmap");
        }

    }

    private void OnGUI()
    {
        // 탭 UI
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentTab == Tab.TextureConversion, "텍스처변환", "Button"))
            currentTab = Tab.TextureConversion;
        if (GUILayout.Toggle(currentTab == Tab.MeshConversion, "메쉬변환", "Button"))
            currentTab = Tab.MeshConversion;
        if (GUILayout.Toggle(currentTab == Tab.LODCreate, "LOD생성", "Button"))
            currentTab = Tab.LODCreate;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Terrain 리스트 및 테이블
        DrawTerrainListWithDetails();

        // 현재 탭에 따라 다른 UI 호출
        switch (currentTab)
        {
            case Tab.TextureConversion:
                DrawTextureConversionTab();
                break;
            case Tab.MeshConversion:
                DrawMeshConversionTab();
                break;
            case Tab.LODCreate:
                DrawLodCreateTab();
                break;
        }

        // 고정된 버튼 (탭별로 다름)
        DrawBottomTab();
    }

    // 생성하는 버튼
    private void DrawBottomTab()
    {
        // 아래쪽 고정 영역
        EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace(); // 공간 밀기
        GUILayout.Space(10);


        // 탭별로 버튼 동작을 나누기
        if (currentTab == Tab.TextureConversion)
        {
            if (textureProcessor.layerTextures.Count > 0)
            {
                if (GUILayout.Button("텍스처 생성"))
                {
                    textureProcessor.GenerateTextureAtlas(baseName, savePath, albedoSuffix, normalSuffix);
                }

            }
            else
            {
                GUILayout.Label("터레인 텍스처를 수집해야 생성 버튼이 활성화 됩니다.");
            }

        }
        if (currentTab == Tab.MeshConversion)
        {
            if (textureProcessor.layerTextures.Count > 0)
            {

                // 실행 버튼
                if (GUILayout.Button("메쉬 생성"))
                {
                    foreach (var terrain in textureProcessor.terrains)
                    {
                        if (terrain == null) continue;
                        string textureName = $"{filePrefix}{terrain.name}";

                        // 지형을 나눌때 지형마다 스플랫맵 생성
                        if (textureProcessor.terrains.Length == 1 && splitOption && !sameMaterial)
                        {
                            textureProcessor.GenerateSplitSplatmaps(terrain, selectedSplitCount, savePath, textureName);
                        }
                        // 스플랫맵 나누지 않음
                        else
                        {
                            textureProcessor.GenerateSingleSplatmap(terrain, savePath, textureName);
                        }

                        // 지형 나누기
                        if (textureProcessor.terrains.Length == 1 && splitOption)
                        {
                            GenerateSplitMeshes(terrain, selectedSplitCount);
                        }
                        // 지형 나누지 않음
                        else
                        {
                            GenerateSingleMesh(terrain);
                        }

                        Debug.Log($"Mesh and material created for Terrain '{terrain.name}' at: {savePath}");
                    }
                }

            }
            else
            {
                GUILayout.Label("터레인 텍스처를 수집해야 생성 버튼이 활성화 됩니다.");
            }
        }
        if (currentTab == Tab.LODCreate)
        {
            if (textureProcessor.layerTextures.Count > 0)
            {
                if (GUILayout.Button("LOD 생성"))
                {
                    foreach (var terrain in textureProcessor.terrains)
                    {
                        string textureName = $"{lodPrefix}{terrain.name}";
                        // -------터레인 하나일때---------
                        // lod메쉬랑 lod텍스처를 모두 나눴을경우
                        if (textureProcessor.terrains.Length == 1 && lodMeshSplit && lodTextureSplit)
                        {
                            textureProcessor.GenerateLodSplitTextures(terrain, selectedSplitCount, lodTextureSize, savePath, textureName, lodNormalTexture);
                            GenerateLodSplitMeshes();
                        }
                        // lod메쉬는 나눴고 텍스처는 메쉬지형과 같은거 사용
                        else if (textureProcessor.terrains.Length == 1 && lodMeshSplit && !lodTexture)
                        {
                            GenerateLodSplitMeshes();
                        }
                        // lod메쉬는 나눴고 lod텍스처는 한장
                        else if (textureProcessor.terrains.Length == 1 && lodMeshSplit && lodTexture)
                        {
                            textureProcessor.GenerateLodTexture(terrain, lodTextureSize, savePath, textureName, lodNormalTexture);
                            GenerateLodSplitMeshes();
                        }
                        // -------터레인 여러개일때---------
                        // lod 텍스처 생성
                        else if (lodTexture)
                        {
                            textureProcessor.GenerateLodTexture(terrain, lodTextureSize, savePath, textureName, lodNormalTexture);
                            GenerateLodMeshes();
                        }
                        // 메쉬지형꺼랑 같은거 사용
                        else
                        {
                            GenerateLodMeshes();
                        }
                    }                  
                }

            }
            else
            {
                GUILayout.Label("터레인 텍스처를 수집해야 생성 버튼이 활성화 됩니다.");
            }

        }

        GUILayout.Space(10);
        EditorGUILayout.EndVertical();
    }


    // 텍스처 변환 UI
    private void DrawTextureConversionTab()
    {
        // 스크롤뷰 시작
        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

        // Texture Array Options
        GUILayout.Label("Texture Array Options", EditorStyles.boldLabel);
        textureProcessor.textureSize = EditorGUILayout.IntPopup(
            "Texture Size",
            textureProcessor.textureSize,
            new string[] { "256", "512", "1024", "2048" },
            new int[] { 256, 512, 1024, 2048 }
        );
        textureProcessor.textureFormat = (TextureFormat)EditorGUILayout.EnumPopup("Texture Format", textureProcessor.textureFormat);
        textureProcessor.useMipMap = EditorGUILayout.Toggle("Use MipMap", textureProcessor.useMipMap);
        textureProcessor.filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", textureProcessor.filterMode);

        GUILayout.Space(10);

        // Save Options
        GUILayout.Label("Save Options", EditorStyles.boldLabel);
        DrawSavePathField();

        baseName = EditorGUILayout.TextField("Base Name", baseName);
        albedoSuffix = EditorGUILayout.TextField("Albedo Suffix", albedoSuffix);
        normalSuffix = EditorGUILayout.TextField("Normal Suffix", normalSuffix);

        // 스크롤뷰 종료
        EditorGUILayout.EndScrollView();

    }

    // 메쉬 변환 UI
    private void DrawMeshConversionTab()
    {
        // 스크롤뷰 시작
        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

        // 쉐이더 선택
        GUILayout.Label("Shader Options", EditorStyles.boldLabel);
        selectedShader = (Shader)EditorGUILayout.ObjectField("Shader", selectedShader, typeof(Shader), false);

        if (selectedShader == null)
        {
            EditorGUILayout.HelpBox($"Default Shader: {defaultShaderName} will be used.", MessageType.Info);
        }

        // 메쉬 해상도 옵션
        GUILayout.Label("Mesh Resolution Options", EditorStyles.boldLabel);

        // 해상도 선택 범위 확장
        int[] resolutions = { 32, 64, 128, 256, 512, 1024, 2048 };
        string[] resolutionLabels = { "32x32", "64x64", "128x128", "256x256 32Bit", "512x512 32Bit", "1024x1024 32Bit", "2048x2048 32Bit" };

        // 기본 해상도 자동 설정
        if (selectedResolution == -1 && textureProcessor.terrains.Length > 0)
        {
            Terrain firstTerrain = textureProcessor.terrains[0];
            if (firstTerrain != null)
            {
                UpdateMeshResolutionBasedOnTerrain(firstTerrain);
            }
        }

        selectedResolution = EditorGUILayout.IntPopup(
            "Mesh Resolution",
            selectedResolution,
            resolutionLabels,
            resolutions
        );

        // 분할 옵션 (터레인 1개일 때만 표시)
        if (textureProcessor.terrains.Length == 1)
        {
            EditorGUILayout.BeginHorizontal();
            splitOption = EditorGUILayout.Toggle("Split", splitOption, GUILayout.Width(170));

            EditorGUI.BeginDisabledGroup(!splitOption);
            selectedSplitCount = Mathf.NextPowerOfTwo(EditorGUILayout.IntSlider(selectedSplitCount, 2, 32));

            EditorGUILayout.EndHorizontal();
            sameMaterial = EditorGUILayout.Toggle("Same Material", sameMaterial);

            EditorGUI.EndDisabledGroup();
        }

        GUILayout.Space(10);

        // Splatmap Options
        GUILayout.Label("Splatmap Options", EditorStyles.boldLabel);
        textureProcessor.splatmapSize = EditorGUILayout.IntPopup(
            "Splatmap Size",
            textureProcessor.splatmapSize,
            new string[] { "Default", "16x16", "32x32", "64x64", "128x128", "256x256", "512x512", "1024x1024", "2048x2048" },
            new int[] { -1, 16, 32, 64, 128, 256, 512, 1024, 2048 }
        );

        GUILayout.Space(10);


        // 저장 경로 설정
        GUILayout.Label("Save Options", EditorStyles.boldLabel);
        DrawSavePathField();

        // 접두사 입력 필드
        filePrefix = EditorGUILayout.TextField("File Prefix", filePrefix);
        GUILayout.Space(10);

        // 하이라키 추가 여부 옵션
        addToHierarchy = EditorGUILayout.Toggle("Add to Hierarchy", addToHierarchy);

        if (addToHierarchy)
        {
            // 하이라키에서 부모 오브젝트를 선택할 수 있도록 ObjectField 제공
            parentObject = (GameObject)EditorGUILayout.ObjectField(
                "Parent Object",
                parentObject,
                typeof(GameObject),
                true // 하이라키에 있는 오브젝트만 선택 가능
            );

            // 부모 오브젝트가 선택되지 않았을 경우, 이름을 입력할 수 있는 필드 표시
            if (parentObject == null)
            {
                parentHierarchyName = EditorGUILayout.TextField("Parent Name", parentHierarchyName);
            }

            GUILayout.Space(10);

            // Mesh Collider 옵션
            addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", addMeshCollider);

            GUILayout.Space(10);

            // Static 옵션
            EditorGUILayout.BeginHorizontal();
            useStatic = EditorGUILayout.Toggle("Static", useStatic, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!useStatic);
            staticFlags = (StaticEditorFlags)EditorGUILayout.EnumFlagsField(staticFlags);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Tag 옵션
            EditorGUILayout.BeginHorizontal();
            useTag = EditorGUILayout.Toggle("Tag", useTag, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!useTag);
            selectedTag = EditorGUILayout.TagField(selectedTag);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Layer 옵션
            EditorGUILayout.BeginHorizontal();
            useLayer = EditorGUILayout.Toggle("Layer", useLayer, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!useLayer);
            selectedLayer = EditorGUILayout.LayerField(selectedLayer);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

        }

        // 스크롤뷰 종료
        EditorGUILayout.EndScrollView();

    }

    /// LOD UI
    private void DrawLodCreateTab()
    {
        // 스크롤뷰 시작
        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

        GUILayout.Label("LOD Options", EditorStyles.boldLabel);
        lodPrefix = EditorGUILayout.TextField("LOD Prefix", lodPrefix);
        terrainLod = EditorGUILayout.IntSlider("Terrain LOD Resolution", terrainLod, 0, 8);




        GUILayout.Space(10);
        GUILayout.Label("Mesh Options", EditorStyles.boldLabel);
        yOffset = EditorGUILayout.FloatField("Y Offset", yOffset);
        edgeDown = EditorGUILayout.Toggle("Edge Down", edgeDown);
        if (edgeDown)
        {
            edgeDownDistance = EditorGUILayout.FloatField("Edge Down Distance", edgeDownDistance);
        }


        GUILayout.Space(10);
        GUILayout.Label("Texture Options", EditorStyles.boldLabel);
        lodTexture = EditorGUILayout.Toggle("LOD Texture", lodTexture);
        if (lodTexture)
        {
            lodNormalTexture = EditorGUILayout.Toggle("LOD Normal Texture", lodNormalTexture);
            lodTextureSize = Mathf.NextPowerOfTwo(EditorGUILayout.IntSlider("LOD Texture Size", lodTextureSize, 64, 2048));
        }

        GUILayout.Space(10);
        // 분할 옵션 (터레인 1개일 때만 표시)
        if (textureProcessor.terrains.Length == 1)
        {
            GUILayout.Label("LOD Split Options", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            lodMeshSplit = EditorGUILayout.Toggle("LOD Mesh Split", lodMeshSplit, GUILayout.Width(170));

            EditorGUI.BeginDisabledGroup(!lodMeshSplit);
            selectedSplitCount = Mathf.NextPowerOfTwo(EditorGUILayout.IntSlider(selectedSplitCount, 2, 32));

            EditorGUILayout.EndHorizontal();

            if (textureProcessor.terrains.Length == 1 && lodTexture)
            {
                lodTextureSplit = EditorGUILayout.Toggle("LOD Texture Split", lodTextureSplit);

            }
            EditorGUI.EndDisabledGroup();
        }


        GUILayout.Space(10);
        GUILayout.Label("Save Options", EditorStyles.boldLabel);
        DrawSavePathField();

        GUILayout.Space(10);
        // 하이라키에 생성 버튼 공유
        addToHierarchy = EditorGUILayout.Toggle("Add to Hierarchy", addToHierarchy);

        if (addToHierarchy)
        {
            // 하이라키에서 부모 오브젝트를 선택할 수 있도록 ObjectField 제공
            lodParentObject = (GameObject)EditorGUILayout.ObjectField(
                "LOD Parent Object",
                lodParentObject,
                typeof(GameObject),
                true // 하이라키에 있는 오브젝트만 선택 가능
            );

            // 부모 오브젝트가 선택되지 않았을 경우, 이름을 입력할 수 있는 필드 표시
            if (lodParentObject == null)
            {
                lodParentHierarchyName = EditorGUILayout.TextField("LOD Parent Name", lodParentHierarchyName);
            }

            GUILayout.Space(10);

            // Mesh Collider 옵션
            lodAddMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", lodAddMeshCollider);

            GUILayout.Space(10);

            // Static 옵션
            EditorGUILayout.BeginHorizontal();
            lodUseStatic = EditorGUILayout.Toggle("Static", lodUseStatic, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!lodUseStatic);
            lodStaticFlags = (StaticEditorFlags)EditorGUILayout.EnumFlagsField(lodStaticFlags);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Tag 옵션
            EditorGUILayout.BeginHorizontal();
            lodUseTag = EditorGUILayout.Toggle("Tag", lodUseTag, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!lodUseTag);
            lodSelectedTag = EditorGUILayout.TagField(lodSelectedTag);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Layer 옵션
            EditorGUILayout.BeginHorizontal();
            lodUseLayer = EditorGUILayout.Toggle("Layer", lodUseLayer, GUILayout.Width(170));
            EditorGUI.BeginDisabledGroup(!lodUseLayer);
            lodSelectedLayer = EditorGUILayout.LayerField(lodSelectedLayer);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

        }

        // 스크롤뷰 종료
        EditorGUILayout.EndScrollView();
    }


    private void InitializeTerrainList()
    {
        terrainList = new ReorderableList(textureProcessor.terrains, typeof(Terrain), true, true, true, true);

        // 리스트 헤더
        terrainList.drawHeaderCallback = (Rect rect) =>
        {
            float[] columnWidths = { rect.width * 0.5f, rect.width * 0.2f, rect.width * 0.2f, rect.width * 0.1f, rect.width * 0.1f };
            EditorGUI.LabelField(new Rect(rect.x, rect.y, columnWidths[0], rect.height), "Terrains");
            EditorGUI.LabelField(new Rect(rect.x + columnWidths[0], rect.y, columnWidths[1], rect.height), "Size");
            EditorGUI.LabelField(new Rect(rect.x + columnWidths[0] + columnWidths[1], rect.y, columnWidths[2], rect.height), "Resolution");
            EditorGUI.LabelField(new Rect(rect.x + columnWidths[0] + columnWidths[1] + columnWidths[2], rect.y, columnWidths[3], rect.height), "Layers");
        };

        // 리스트 항목 그리기
        terrainList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            float[] columnWidths = { rect.width * 0.5f, rect.width * 0.2f, rect.width * 0.2f, rect.width * 0.1f, rect.width * 0.1f };
            rect.y += 2;

            // Terrain 드롭다운 필드
            textureProcessor.terrains[index] = (Terrain)EditorGUI.ObjectField(
                new Rect(rect.x, rect.y, columnWidths[0], EditorGUIUtility.singleLineHeight),
                textureProcessor.terrains[index],
                typeof(Terrain),
                true
            );

            if (textureProcessor.terrains[index] != null)
            {
                TerrainData data = textureProcessor.terrains[index].terrainData;

                // Size 출력
                if (data != null)
                {
                    EditorGUI.LabelField(new Rect(rect.x + columnWidths[0], rect.y, columnWidths[1], EditorGUIUtility.singleLineHeight),
                        $"{data.size.x} x {data.size.z}");

                    // Resolution 출력
                    EditorGUI.LabelField(new Rect(rect.x + columnWidths[0] + columnWidths[1], rect.y, columnWidths[2], EditorGUIUtility.singleLineHeight),
                        $"{data.heightmapResolution}x{data.heightmapResolution}");

                    // Layers 출력
                    EditorGUI.LabelField(new Rect(rect.x + columnWidths[0] + columnWidths[1] + columnWidths[2], rect.y, columnWidths[3], EditorGUIUtility.singleLineHeight),
                        data.terrainLayers.Length.ToString());
                }
            }
        };

        // 항목 추가
        terrainList.onAddCallback = (ReorderableList list) =>
        {
            AddTerrain(null);
        };

        // 항목 삭제
        terrainList.onRemoveCallback = (ReorderableList list) =>
        {
            RemoveTerrain(list.index);
        };
    }


    private void DrawTerrainListWithDetails()
    {
        // 테이블 스타일 설정
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) {
            alignment = TextAnchor.MiddleCenter // 가운데 정렬
        };

        GUIStyle cellStyle = new GUIStyle(EditorStyles.label) {
            alignment = TextAnchor.MiddleCenter, // 가운데 정렬
            wordWrap = false // 텍스트 줄바꿈 방지
        };

        // 테이블 헤더
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Terrains", headerStyle, GUILayout.Width(position.width * 0.4f)); // Terrain 열 (윈도우 크기의 40%)
        GUILayout.Label("Size", headerStyle, GUILayout.Width(position.width * 0.2f));     // Size 열 (윈도우 크기의 20%)
        GUILayout.Label("Resolution", headerStyle, GUILayout.Width(position.width * 0.2f)); // Resolution 열 (윈도우 크기의 20%)
        GUILayout.Label("Layers", headerStyle, GUILayout.Width(position.width * 0.1f));  // Layers 열 (윈도우 크기의 10%)

        // "모두 삭제" 버튼
        if (textureProcessor.terrains.Length > 0 && GUILayout.Button("X", GUILayout.Width(30)))
        {
            RemoveAllTerrains(); // 모두 삭제 함수 호출
            GUIUtility.ExitGUI(); // UI 루프 종료
            return;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5); // 간격 추가

        if (textureProcessor.terrains.Length == 0)
        {
            // Terrain이 없을 때
            Rect dropArea = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true)); // 드래그 앤 드롭 영역
            GUI.Box(dropArea, "드래그 앤 드롭으로 Terrain을 추가하세요.", EditorStyles.helpBox);

            HandleDragAndDrop(dropArea); // 드래그 앤 드롭 처리
        }
        else
        {
            // Terrain이 있을 때 테이블 출력
            Rect tableArea = GUILayoutUtility.GetRect(position.width, 120); // 테이블 영역
            GUI.Box(tableArea, GUIContent.none); // 테이블 배경 박스 (드래그 영역)

            HandleDragAndDrop(tableArea); // 👈 테이블 자체를 드래그 앤 드롭 영역으로 설정

            scrollPos = GUI.BeginScrollView(tableArea, scrollPos, new Rect(0, 0, position.width - 20, textureProcessor.terrains.Length * 25));

            for (int i = 0; i < textureProcessor.terrains.Length; i++)
            {
                Terrain terrain = textureProcessor.terrains[i];
                if (terrain == null) continue;

                TerrainData data = terrain.terrainData;
                if (data == null) continue;

                // 줄무늬 효과 적용
                GUI.backgroundColor = i % 2 == 0 ? new Color(0.9f, 0.9f, 0.9f) : Color.white;

                Rect rowRect = new Rect(0, i * 25, position.width, 25);
                GUI.Box(rowRect, GUIContent.none); // 배경 박스

                EditorGUILayout.BeginHorizontal();

                // Terrain Object 필드
                textureProcessor.terrains[i] = (Terrain)EditorGUI.ObjectField(
                    new Rect(rowRect.x, rowRect.y, position.width * 0.4f, 20),
                    terrain,
                    typeof(Terrain),
                    true
                );

                // Size
                EditorGUI.LabelField(
                    new Rect(rowRect.x + position.width * 0.4f, rowRect.y, position.width * 0.2f, 20),
                    $"{data.size.x} x {data.size.z}",
                    cellStyle
                );

                // Resolution
                EditorGUI.LabelField(
                    new Rect(rowRect.x + position.width * 0.6f, rowRect.y, position.width * 0.2f, 20),
                    $"{data.heightmapResolution}x{data.heightmapResolution}",
                    cellStyle
                );

                // Layers
                EditorGUI.LabelField(
                    new Rect(rowRect.x + position.width * 0.8f, rowRect.y, position.width * 0.1f, 20),
                    data.terrainLayers.Length.ToString(),
                    cellStyle
                );

                // 삭제 버튼
                if (GUI.Button(new Rect(rowRect.x + position.width * 0.9f, rowRect.y, 30, 20), "X"))
                {
                    RemoveTerrain(i); // 삭제 함수 호출
                    GUIUtility.ExitGUI(); // 즉시 UI 루프 종료
                    return; // 오류 방지를 위해 함수 종료
                }

                EditorGUILayout.EndHorizontal();
            }

            GUI.EndScrollView();
        }

        GUILayout.Space(5);

        // Collect Textures
        if (GUILayout.Button("터레인 텍스처 수집"))
        {
            textureProcessor.CollectUniqueTexturesByLayer();
        }

        if (textureProcessor.layerTextures.Count > 0)
        {

            GUILayout.Label("수집한 텍스처 순서");

            // 고정된 스크롤뷰 최대 높이
            float maxHeight = 80f; // 최대 높이 고정 (4줄 정도)
            float rowHeight = 15f; // 각 줄의 높이

            // 레이어 개수에 따른 전체 콘텐츠 높이 계산
            float totalHeight = textureProcessor.layerTextures.Count * rowHeight;

            // 스크롤뷰 영역 (고정된 높이로 설정)
            Rect scrollViewRect = GUILayoutUtility.GetRect(0, maxHeight, GUILayout.ExpandWidth(true));

            // 스크롤뷰 시작
            textureScrollPos = GUI.BeginScrollView(scrollViewRect, textureScrollPos, new Rect(0, 0, scrollViewRect.width - 20, totalHeight));

            // 배경 박스 (전체 콘텐츠 높이 기준으로 설정)
            GUI.Box(new Rect(0, 0, scrollViewRect.width, totalHeight), GUIContent.none);

            // 레이어 텍스트 출력
            for (int i = 0; i < textureProcessor.layerTextures.Count; i++)
            {
                var layer = textureProcessor.layerTextures[i];

                // 개별 줄의 위치 계산
                Rect lineRect = new Rect(5, i * rowHeight, scrollViewRect.width - 10, rowHeight);
                GUI.Label(lineRect, $"{i + 1}. {layer.name} | Albedo: {(layer.albedo ? layer.albedo.name : "None")} | Normal: {(layer.normal ? layer.normal.name : "None")}");
            }

            // 스크롤뷰 종료
            GUI.EndScrollView();


        }

        GUILayout.Space(5); // 추가 간격
    }


    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (!dropArea.Contains(evt.mousePosition)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    // 부모 오브젝트일 경우 자식까지 포함하여 처리
                    if (obj is GameObject go)
                    {
                        // 부모와 모든 자식에 포함된 Terrain을 처리
                        AddTerrainsFromGameObject(go);
                    }
                    else if (obj is Terrain terrain) // 직접 Terrain이 드래그된 경우
                    {
                        AddTerrain(terrain);
                    }
                }

                evt.Use(); // 이벤트 처리 완료
            }
        }
    }



    private void AddTerrain(Terrain terrain)
    {
        if (terrain == null || System.Array.Exists(textureProcessor.terrains, t => t == terrain))
        {
            Debug.LogWarning("이미 추가된 Terrain입니다.");
            return;
        }

        int newCount = textureProcessor.terrains.Length + 1;
        var newTerrains = new Terrain[newCount];
        textureProcessor.terrains.CopyTo(newTerrains, 0);
        newTerrains[newCount - 1] = terrain;
        textureProcessor.terrains = newTerrains;

        terrainList.list = textureProcessor.terrains;

        // 최초로 추가된 Terrain에만 Mesh Resolution 설정
        if (!isMeshResolutionSet)
        {
            UpdateMeshResolutionBasedOnTerrain(terrain);
            isMeshResolutionSet = true; // 설정 완료 플래그
        }

        Repaint();
    }

    private void UpdateMeshResolutionBasedOnTerrain(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null) return;

        int heightmapResolution = terrain.terrainData.heightmapResolution;
        selectedResolution = Mathf.NextPowerOfTwo(heightmapResolution) / 2; // 65x65는 64로 조정
    }


    private void AddTerrainsFromGameObject(GameObject parent)
    {
        // 부모와 자식 오브젝트에 있는 모든 Terrain 컴포넌트를 가져옴
        Terrain[] terrains = parent.GetComponentsInChildren<Terrain>();

        foreach (var terrain in terrains)
        {
            AddTerrain(terrain); // 이미 추가된 Terrain은 AddTerrain 함수에서 중복 처리됨
        }
    }


    private void RemoveTerrain(int index)
    {
        var newTerrains = new Terrain[textureProcessor.terrains.Length - 1];
        for (int i = 0, j = 0; i < textureProcessor.terrains.Length; i++)
        {
            if (i == index) continue;
            newTerrains[j++] = textureProcessor.terrains[i];
        }
        textureProcessor.terrains = newTerrains;

        terrainList.list = textureProcessor.terrains;

        // 첫 번째 Terrain의 해상도로 Mesh Resolution을 재설정
        if (textureProcessor.terrains.Length > 0)
        {
            UpdateMeshResolutionBasedOnTerrain(textureProcessor.terrains[0]);
        }
        else
        {
            selectedResolution = -1; // 해상도 초기화
        }

        Repaint();
    }
    private void RemoveAllTerrains()
    {
        textureProcessor.terrains = new Terrain[0]; // Terrain 배열 초기화
        terrainList.list = textureProcessor.terrains; // 리스트 갱신

        isMeshResolutionSet = false; // 모든 Terrain을 제거하면 초기화
        selectedResolution = -1;    // 해상도 초기화

        Repaint(); // UI 갱신
    }

    private void DrawSavePathField()
    {
        EditorGUILayout.BeginHorizontal();
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Save Folder", savePath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogWarning("저장 경로는 프로젝트 내부여야 합니다.");
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }


    // 메쉬 생성
    private void GenerateSingleMesh(Terrain terrain)
    {
        {
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Invalid Terrain.");
                return;
            }
            var layerIndexMap = textureProcessor.CreateLayerIndexMap();

            // 메쉬 생성
            Mesh generatedMesh = TerrainMeshProcessor.GenerateMesh(terrain, selectedResolution);

            string fileName = terrain.name;
            string prefixedName = $"{filePrefix}{fileName}";

            TerrainMeshProcessor.SaveMeshAsOBJ(generatedMesh, savePath, prefixedName);

            // 저장된 Mesh를 프로젝트에서 불러오기
            string meshAssetPath = Path.Combine(savePath, $"{prefixedName}.obj"); // obj 경로 생성
            meshAssetPath = meshAssetPath.Replace("\\", "/"); // 경로 슬래시 표준화

            // Mesh를 AssetDatabase에서 불러오기
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (savedMesh == null)
            {
                Debug.LogError($"Failed to load Mesh Asset at: {meshAssetPath}");
                return;
            }

            // 매트리얼 생성
            Material newMaterial = TerrainMaterialProcessor.CreateMaterial(savePath, prefixedName, selectedShader);

            // 스플랫맵 연결
            int splatmapCount = Mathf.CeilToInt((float)terrain.terrainData.alphamapLayers / 4);
            TerrainMaterialProcessor.AssignSplatmapsToMaterial(newMaterial, savePath, filePrefix + fileName, splatmapCount);

            // 어레이맵 넣기
            TerrainMaterialProcessor.AssignArrayToMaterial(newMaterial, savePath, baseName, albedoSuffix, normalSuffix);

            // 터레인 레이어 기반 설정
            TerrainMaterialProcessor.ConfigureMaterialForTerrainLayers(newMaterial, terrain.terrainData.terrainLayers, layerIndexMap, normalStrength: 1.0f);

            // UV 옵셋
            TerrainMaterialProcessor.AssignUVScaleAndOffset(newMaterial, terrain.terrainData.terrainLayers, terrain.terrainData, 1);

            // 하이라키에 추가
            if (addToHierarchy)
            {
                // Static, Tag, Layer 설정값 전달
                string parentName = parentObject != null ? parentObject.name : parentHierarchyName;
                TerrainHierarchyProcessor.AddObjectToHierarchy(
                    objectName: prefixedName,
                    mesh: savedMesh,
                    material: newMaterial,
                    parentName: parentName,
                    staticFlags: useStatic ? staticFlags : (StaticEditorFlags?)null,
                    tag: useTag ? selectedTag : null,
                    layer: useLayer ? selectedLayer : -1,
                    addMeshCollider: addMeshCollider,
                    position: terrain.transform.position // Terrain 위치 전달
                );
            }
        }
    }

    // 메쉬 나누면서 생성
    private void GenerateSplitMeshes(Terrain terrain, int splitCount)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("Invalid Terrain.");
            return;
        }
        var layerIndexMap = textureProcessor.CreateLayerIndexMap();

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainSize = terrainData.size;

        float chunkWidth = terrainSize.x / splitCount;
        float chunkHeight = terrainSize.z / splitCount;
        int chunkResolution = terrainData.heightmapResolution / splitCount;

        // 매트리얼 생성 (같은 매트리얼 옵션 체크)
        Material sharedMaterial = null;
        string materialName = $"{filePrefix}{terrain.name}";
        if (sameMaterial)
        {
            sharedMaterial = TerrainMaterialProcessor.CreateMaterial(savePath, materialName, selectedShader);

            // 스플랫맵 연결
            int splatmapCount = Mathf.CeilToInt((float)terrain.terrainData.alphamapLayers / 4);
            TerrainMaterialProcessor.AssignSplatmapsToMaterial(sharedMaterial, savePath, materialName, splatmapCount);

            // 텍스처 어레이 연결
            TerrainMaterialProcessor.AssignArrayToMaterial(sharedMaterial, savePath, baseName, albedoSuffix, normalSuffix);

            // 터레인 레이어 기반 설정
            TerrainMaterialProcessor.ConfigureMaterialForTerrainLayers(sharedMaterial, terrain.terrainData.terrainLayers, layerIndexMap, normalStrength: 1.0f);

            // UV 옵셋
            TerrainMaterialProcessor.AssignUVScaleAndOffset(sharedMaterial, terrain.terrainData.terrainLayers, terrain.terrainData, 1);
        }


        // 터레인 월드 좌표
        Vector3 terrainPosition = terrain.transform.position;

        for (int z = 0; z < splitCount; z++)
        {
            for (int x = 0; x < splitCount; x++)
            {
                int startX = x * chunkResolution;
                int startZ = z * chunkResolution;

                float[,] heights = terrainData.GetHeights(startX, startZ, chunkResolution + 1, chunkResolution + 1);
                Mesh chunkMesh = TerrainMeshProcessor.GenerateMeshFromHeights(heights, chunkWidth, chunkHeight, terrainSize.y);

                // UV 조정 (같은 매트리얼 vs 개별 매트리얼)
                if (sameMaterial)
                {
                    chunkMesh = TerrainMeshProcessor.AdjustUVForSplitMesh(chunkMesh, x, z, splitCount, terrainSize.x, terrainSize.z);
                }

                string chunkName = $"{filePrefix}{terrain.name}_{x}_{z}";
                TerrainMeshProcessor.SaveMeshAsOBJ(chunkMesh, savePath, chunkName);

                // 저장된 메쉬 불러오기
                string meshAssetPath = Path.Combine(savePath, $"{chunkName}.obj");
                meshAssetPath = meshAssetPath.Replace("\\", "/");
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

                if (savedMesh == null)
                {
                    Debug.LogError($"Failed to load mesh at: {meshAssetPath}");
                    continue;
                }

                // 매트리얼 선택: 같은 매트리얼 재사용 또는 개별 생성
                Material materialToUse = sharedMaterial;
                if (!sameMaterial)
                {
                    materialToUse = TerrainMaterialProcessor.CreateMaterial(savePath, chunkName, selectedShader);

                    // 스플랫맵 연결
                    int splatmapCount = Mathf.CeilToInt((float)terrain.terrainData.alphamapLayers / 4);
                    TerrainMaterialProcessor.AssignSplatmapsToMaterial(materialToUse, savePath, chunkName, splatmapCount);

                    // 텍스처 어레이 연결
                    TerrainMaterialProcessor.AssignArrayToMaterial(materialToUse, savePath, baseName, albedoSuffix, normalSuffix);

                    // 터레인 레이어 기반 설정
                    TerrainMaterialProcessor.ConfigureMaterialForTerrainLayers(materialToUse, terrain.terrainData.terrainLayers, layerIndexMap, normalStrength: 1.0f);

                    // UV 옵셋
                    TerrainMaterialProcessor.AssignUVScaleAndOffset(materialToUse, terrain.terrainData.terrainLayers, terrain.terrainData, selectedSplitCount);
                }

                // 하이라키에 추가
                if (addToHierarchy)
                {
                    string parentName = parentObject != null ? parentObject.name : parentHierarchyName;
                    TerrainHierarchyProcessor.AddObjectToHierarchy(
                    chunkName,
                    savedMesh, // 저장된 메쉬를 사용
                    materialToUse,
                    parentName: parentName,
                    staticFlags: useStatic ? staticFlags : (StaticEditorFlags?)null,
                    tag: useTag ? selectedTag : null,
                    layer: useLayer ? selectedLayer : -1,
                    addMeshCollider: addMeshCollider,
                    position: new Vector3(
                        terrainPosition.x + x * chunkWidth,
                        terrainPosition.y,
                        terrainPosition.z + z * chunkHeight
                        )

                    );
                }

                Debug.Log($"Chunk created: {chunkName} (Material: {materialToUse.name})");
            }
        }
    }

    // LOD 생성
    private void GenerateLodMeshes()
    {
        if (textureProcessor.terrains == null || textureProcessor.terrains.Length == 0)
        {
            Debug.LogWarning("No terrains available for LOD generation.");
            return;
        }

        foreach (var terrain in textureProcessor.terrains)
        {
            if (terrain == null) continue;

            string prefixedName = $"{lodPrefix}{terrain.name}";

            // LOD 메쉬 생성
            Mesh lodMesh = TerrainMeshProcessor.GenerateLodMesh(terrain, terrainLod, selectedResolution, yOffset, edgeDown, edgeDownDistance);
            TerrainMeshProcessor.SaveMeshAsOBJ(lodMesh, savePath, prefixedName);

            Material lodMaterial = null;
            // LOD 텍스처 생성 (옵션)
            if (lodTexture)
            {
                // LOD 매트리얼 생성
                selectedLitShader = Shader.Find("Universal Render Pipeline/Lit");
                lodMaterial = TerrainMaterialProcessor.CreateMaterial(savePath, prefixedName, selectedLitShader);
                TerrainMaterialProcessor.ConfigureLodMaterial(lodMaterial, savePath, prefixedName, lodNormalTexture);
            }
            else
            {
                string materialPath = Path.Combine(savePath, $"{filePrefix}{terrain.name}.mat");
                lodMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (lodMaterial == null)
                {
                    Debug.LogWarning($"매테리얼 없음: {materialPath}");
                }
            }
            // 저장된 Mesh를 프로젝트에서 불러오기
            string meshAssetPath = Path.Combine(savePath, $"{prefixedName}.obj"); // obj 경로 생성
            meshAssetPath = meshAssetPath.Replace("\\", "/"); // 경로 슬래시 표준화

            // Mesh를 AssetDatabase에서 불러오기
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

            string lodObjectName = $"{lodPrefix}{terrain.name}";
            string parentName = parentObject != null ? parentObject.name : parentHierarchyName;

            if (addToHierarchy)
            {
                TerrainHierarchyProcessor.AddObjectToHierarchy(
                    objectName: lodObjectName,
                    mesh: savedMesh,
                    material: lodMaterial,
                    parentName: parentName,
                    staticFlags: lodUseStatic ? lodStaticFlags : (StaticEditorFlags?)null,
                    tag: lodUseTag ? lodSelectedTag : null,
                    layer: lodUseLayer ? lodSelectedLayer : -1,
                    addMeshCollider: lodAddMeshCollider,
                    position: terrain.transform.position // Terrain 위치 전달
                );
            }

            Debug.Log($"LOD {terrainLod} created for Terrain: {terrain.name}");
        }

        Debug.Log("LOD generation completed for all terrains.");

    }

    // LOD 나누면서 생성
    private void GenerateLodSplitMeshes()
    {
        if (textureProcessor.terrains == null || textureProcessor.terrains.Length == 0)
        {
            Debug.LogWarning("No terrains available for LOD generation.");
            return;
        }

        foreach (var terrain in textureProcessor.terrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;

            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainSize = terrainData.size;

            // 분할할 조각 크기 계산
            float chunkWidth = terrainSize.x / selectedSplitCount;
            float chunkHeight = terrainSize.z / selectedSplitCount;
            int chunkResolution = terrainData.heightmapResolution / selectedSplitCount;

            selectedLitShader = Shader.Find("Universal Render Pipeline/Lit");
            string materialName = $"{lodPrefix}{terrain.name}";
            Material lodMaterial = null;

            // 매트리얼 중복생성 안하려고 밖에다가 뺌
            if (lodTexture && lodMeshSplit && !lodTextureSplit)
            {
                lodMaterial = TerrainMaterialProcessor.CreateMaterial(savePath, materialName, selectedLitShader);
                TerrainMaterialProcessor.ConfigureLodMaterial(lodMaterial, savePath, materialName, lodNormalTexture);
            }
            else if (!lodTexture && lodMeshSplit)
            {
                string materialPath = Path.Combine(savePath, $"{filePrefix}{terrain.name}.mat");
                lodMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (lodMaterial == null)
                {
                    Debug.LogWarning($"매테리얼 없음: {materialPath}");
                }
            }
            // 터레인 월드 위치
            Vector3 terrainPosition = terrain.transform.position;


            for (int z = 0; z < selectedSplitCount; z++)
            {
                for (int x = 0; x < selectedSplitCount; x++)
                {
                    // 조각별 높이 데이터 추출
                    int startX = x * chunkResolution;
                    int startZ = z * chunkResolution;

                    float[,] chunkHeights = terrainData.GetHeights(startX, startZ, chunkResolution + 1, chunkResolution + 1);

                    // UV 범위 설정
                    Rect uvBounds = new Rect(
                        (float)x / selectedSplitCount,
                        (float)z / selectedSplitCount,
                        1.0f / selectedSplitCount,
                        1.0f / selectedSplitCount
                        );

                    // 조각의 메시 생성
                    Mesh chunkMesh = TerrainMeshProcessor.GenerateLodSplitMesh(
                        chunkHeights,
                        terrainData,
                        chunkWidth,
                        chunkHeight,
                        terrainSize.y,
                        terrainLod,
                        yOffset,
                        edgeDown,
                        edgeDownDistance,
                        uvBounds
                    );

                    if ((!lodTexture || !lodTextureSplit) && lodMeshSplit)
                    {
                        chunkMesh = TerrainMeshProcessor.AdjustUVForSplitMesh(chunkMesh, x, z, selectedSplitCount, terrainSize.x, terrainSize.z);
                    }

                    // 파일 이름 생성

                    string chunkName = $"{lodPrefix}{terrain.name}_{x}_{z}";
                    TerrainMeshProcessor.SaveMeshAsOBJ(chunkMesh, savePath, chunkName);

                    // 저장된 Mesh 불러오기
                    string meshAssetPath = Path.Combine(savePath, $"{chunkName}.obj");
                    meshAssetPath = meshAssetPath.Replace("\\", "/");
                    Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

                    // 매테리얼 생성 및 설정                    
                    if (lodTexture && lodMeshSplit && lodTextureSplit)
                    {
                        lodMaterial = TerrainMaterialProcessor.CreateMaterial(savePath, chunkName, selectedLitShader);
                        TerrainMaterialProcessor.ConfigureLodMaterial(lodMaterial, savePath, chunkName, lodNormalTexture);
                    }

                    // 하이라키에 추가
                    if (addToHierarchy)
                    {
                        TerrainHierarchyProcessor.AddObjectToHierarchy(
                            chunkName,
                            savedMesh,
                            lodMaterial,
                            parentName: lodParentObject != null ? lodParentObject.name : lodParentHierarchyName,
                            staticFlags: lodUseStatic ? lodStaticFlags : (StaticEditorFlags?)null,
                            tag: lodUseTag ? lodSelectedTag : null,
                            layer: lodUseLayer ? lodSelectedLayer : -1,
                            addMeshCollider: lodAddMeshCollider,
                            position: new Vector3(
                                terrainPosition.x + x * chunkWidth,
                                terrainPosition.y,
                                terrainPosition.z + z * chunkHeight
                            )
                        );
                    }

                    Debug.Log($"LOD Chunk created: {chunkName}");
                }
            }
        }

        Debug.Log("LOD Split generation completed for all terrains.");
    }
}
