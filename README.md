Terrain To Mesh Conversion
=======
터레인을 메쉬로 변경해주는 툴 입니다.   
특정 기기에서 한 쉐이더 안에 텍스처를 16장이상을 사용하게되면 비정상적으로 나오는 현상이 있어서 텍스처 어레이로 더 많은 텍스처를 사용할수 있게 하였습니다.   
그리고 제작 방식에 따라 터레인을 여러개로 나눈뒤 서로 다른 레이어를 사용하는 경우가 있어서 자동으로 각 매트리얼 별로 어레이 번호를 자동으로 입력하게 하였습니다.

![easyme](/img/1.png)  
메뉴에서 HY -> 터레인을 메쉬로 변환툴 눌러보면 위 이미지 처럼 나옵니다.

씬에 있는 터레인을 드래그 앤 드롭으로 넣으면 터레인이 등록이 되고 터레인 텍스처 수집을 누르면 텍스처 순서가 나오고 맨 아래 버튼이 활성화 됩니다.

##### 터레인 목록
터레인을 하나나 여러개를 등록할수 있습니다.   
Size는 터레인크기, Resolution은 터레인 헤이트, Layers는 사용중인 레이어 갯수입니다. 

텍스처 변환
-----
![easyme](/img/2.png)  
##### Texture Array Options
Texture Size : 텍스처 어레이에 사용될 텍스처 사이즈입니다. 어레이 특성상 서로 다른 사이즈를 따로따로 지정할 수 없기 때문에 모두 같은 사이즈로 지정합니다.   

Texture Format : 파일 포맷을 정할수 있는데 그냥 RGBA32로 둡니다.   

Use MipMap : 밉맵 사용여부 입니다. 기본적인 밉맵은 작동하지만 텍스처 어레이 특성상 글로벌 밉맵은 작동하지 않으니 주의 하시기 바랍니다.   

##### Save Options
Save Path : 저장될 위치를 지정합니다. ...버튼을 누르면 다른 위치에 지정할수 있습니다.   

Base Name : 저장될 텍스처 어레이의 이름입니다.

Albedo Suffix : 알베도 텍스처가 저장될 어레이 이름 뒤에 들어갈 이름입니다. (RGB)에는 베이스맵 (A)는 스무스니스 맵이 들어갑니다.  
예) TerrainTextureArray_Albedo   

Normal Suffix : 노말 텍스처가 저장될 어레이 이름 뒤에 들어갈 이름입니다.   
예) TerrainTextureArray_Normal
   
![easyme](/img/sample1.png)  
텍스처 생성 버튼을 누르면 위 이미지 처럼 나올 것 입니다.   
Asset으로 생성되는것 보단 임포트 설정이 더 자유로울것 같아서 PNG로 생성하였습니다.

메쉬 변환
-----
![easyme](/img/3.png)  
##### Shader Options
shader : 메쉬 지형에 사용할 쉐이더입니다. 레퍼런스만 맞으면 직접 만든 쉐이더를 사용하셔도 되지만 가능하면 바꾸지 않는것을 추천합니다.

##### Mesh Resolution Options
Mesh Resolution : 메쉬 지형의 폴리곤 숫자를 결정합니다. 만약 터레인의 Resolution이 513x513 이라면 512x512로 지정합니다.   

Split : 만약 터레인이 하나만 등록되어 있다면 나오는 메뉴입니다. 몇등분 할지 정해줍니다. 만약 8로 정했다면 가로8 세로8로 64등분이 됩니다.   

Same Material : 지형이 매트리얼을 모두 같은것을 사용할지 따로따로 사용할지 정합니다. 체크가 되어있지 않다면 각 지형별로 스플랫 맵이 따로따로 생성됩니다.   

##### Splatmap Options
Splatmap Size : 스플랫맵 사이즈를 지정합니다. 만약 Split을 체크하고 Same Material을 해제 했다면 지정된 사이즈에서 나뉘어지게 됩니다.   

##### Save Options
Save Path : 저장될 위치입니다.   

File Prefix : 터레인 이름앞에 붙을 접두사입니다.   
예) 단일 : terrain_Terrain   
    여러개 : terrain_Terrain_0_1   

Add To Hierarchy : 이 옵션을 체크하면 하이라키에 자동으로 불러오는 옵션이 활성화 됩니다.  

Parent Object : 하이라키의 오브젝트를 넣으면 이 오브젝트 안에 생성됩니다.    

Parent Name : 부모를 자동으로 생성하려면 부모 이름을 넣습니다. 만약 비어있다면 부모 없이 생성됩니다. 

Add Mesh Collider : 하이라키로 생성할때 메쉬콜라이더 컴포넌트를 자동으로 생성해줍니다.    

Static : 생성할때 스테틱을 체크해줍니다.  

Tag : 생성할때 테그를 정해줍니다.   

Layer : 생성할때 레이어를 정해줍니다.

LOD 생성
-----
![easyme](/img/4.png)  
##### LOD Options
LOD Prefix : LOD이름 앞에 붙을 접두사입니다.    
예) LOD_Terrain

Terrain LOD Resolution : LOD사이즈를 지정합니다. 만약 3으로 햇을경우 메쉬변환 했을때 512로 했다면 64로 줄어듭니다.    

##### Mesh Options
Y Offset : LOD를 생성할때 기존 지형보다 높이거나 낮추고 싶다면 숫자를 입력합니다.    

Edge Down : LOD를 생성하면서 굴곡이 심할경우 틈이 발생할수 있어서 끝부분에 메쉬를 생성해서 어색함을 줄여줍니다.   

Edge Down Distance : 끝부분에 생성될 메쉬의 사이즈입니다. 20으로 했을경우 20미터가 생성됩니다.

##### Texture Options
LOD Texture : LOD용 텍스처를 생성합니다.  
체크 했다면 LOD용 텍스처가 생성되고 릿쉐이더 기반으로 단순화된 텍스처가 생성됩니다.    
만약 해제했다면 메쉬 지형과 같은 매트리얼을 사용합니다.  

LOD Normal Texture : LOD용 노말 텍스처 생성 여부를 결정합니다.  

LOD Texture Size : LOD용 텍스처 사이즈를 지정합니다.

##### LOD Split Options
LOD Mesh Split : 터레인이 하나만 등록되었을경우에 활성화됩니다. 체크했을경우 메쉬 생성때 처럼 몇등분 할지 정해줍니다.   

LOD Texture Split : 체크했을경우 나눠진 모든 LOD별로 텍스처가 생성됩니다. 
해제 했을경우 모든 LOD가 같은 텍스처와 매트리얼을 사용합니다.    

##### Save Options
Save Path : 저장할 위치를 지정합니다.  

Add To Hierarchy : 하이라이키에 가져옵니다.    

LOD Parent Object : LOD오브젝트를 넣을 부모를 지정합니다.  

LOD Parent Name : 부모 이름을 지정합니다. 비어있다면 부모없이 LOD오브젝트가 하이라키에 생성됩니다.    

Add Mesh Collider : 메쉬 콜라이더 컴포넌트를 생성할지 여부입니다.   

Static : 스테틱 체크 옵션입니다.  

Tag : 테그 체크 옵션입니다.  

Layer : 레이어 체크 옵션입니다.

