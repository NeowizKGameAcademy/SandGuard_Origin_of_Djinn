# 매몰 이전 신전 도시 — 평면 콘셉트 v1

- 제작: 내장 image_gen, 실제 Level 씬 수직 캡처를 구조 참조로 사용.
- 이미지: temple-city-before-burial-plan-v1.png
- 참조: ../LevelArt/Captures/Level-vertical-topview.png
- 사용자가 확정한 설정: 과거 도시가 신전 주위에 있었으며, 불명의 이유로 도시가 모래에 묻히고 신전은 마법의 모래폭풍으로 봉인되어 남았다.
- 이번 제안: 매몰 이전, 신전을 중심으로 순례와 교역이 모이는 오아시스 도시. 남쪽 관문과 상단 숙소 → 시장/행렬 대로 → 신전 성역. 서쪽 저수지·경작지, 동쪽 공방·창고, 북쪽 궁정·사제 구역, 성벽 밖 묘역.
- 원인을 새로 확정하지 않았다. 도시 기능과 배치는 창작 제안이며 역사 도시의 복원이 아니다.
- 이미지 안 축척 막대는 생성된 장식으로 실제 거리를 보증하지 않는다. 구상 기준은 신전 폭 약 160m, 도시 폭 약 0.9–1.1km. Unity 배치 시 실제 치수에 맞춰 재정렬해야 한다.
- 신전의 사각 외곽, 중앙 단상과 대각 계단을 시각적으로 참조했다. 생성 이미지의 세부 계단/출입구는 원본과 완전히 일치하는 설계도가 아니다.

## 참고 자료와 차용한 원리

1. 아마르나: 왕실 도로와 신전·궁정·행정 구역을 연결하는 기념비적 축.
   https://www.metmuseum.org/essays/art-architecture-and-the-city-in-the-reign-of-amenhotep-iv-akhenaten-ca-13531336-b-c
2. 팔미라: 대상 교역 오아시스, 열주 대로와 공공 건물·도시 구역의 연결.
   https://whc.unesco.org/en/list/23
3. 시밤: 성벽 안 밀집 흙벽돌 주거, 거리와 광장, 주변 관개 경작지와의 관계. 후대 도시로서 형태 참고이며 시대 고증 복제 아님.
   https://whc.unesco.org/en/list/192
4. 페트라: 건조 환경에서 수로·저수조·저수지를 이용한 물 관리.
   https://whc.unesco.org/en/list/326
5. 팔미라의 역사 지도 안내: 발굴 이전 유구를 기록한 평면도이며 번성기 전체 복원과 구분.
   https://www.getty.edu/research/exhibitions_events/exhibitions/palmyra/city_plan_base.html
6. UCL 아마르나 도시 자료:
   https://www.ucl.ac.uk/museums-static/digitalegypt/amarna/index.html

## 최종 생성 프롬프트

Use case: stylized-concept.
Create ONE detailed, polished illustrated architectural CITY SITE PLAN for the fantasy game SandGuard: Origin of Djinn. The user wants an imaginative reconstruction of the flourishing desert city that once surrounded their existing temple, BEFORE a mysterious catastrophe buried the city in sand and left the temple sealed by a magical sandstorm. Depict ONLY the intact city before the catastrophe: no storm, no destruction, no burial, no magic barrier. Do not invent the cause of its future destruction.
INPUT IMAGE ROLE: the provided image is an actual exact vertical screenshot of the existing Unity temple level and is the REQUIRED geometry reference for the central temple. Expand the scope to the surrounding city, not a redesign of this temple. Ignore the reference's orange/red translucent circle, yellow selection square, small gray debug blocks and cursor mark: these are game/editor overlays and must not appear.
CRITICAL TEMPLE INVARIANTS: keep its north-up axis-aligned square footprint, four corner entrance landings/stairs, outer north-south stairs along left and right perimeter, mirrored diagonal zigzag stair/bridge networks on the east and west, polygonal junction platforms, north and south axial platforms, and the small central square highest core platform with straight north-south stairs. It is the unusual open terraced temple visible in the reference, NOT a standard smooth pyramid, NOT a roofed cathedral, NOT concentric square terraces. Draw this exact recognizable circulation pattern as clean architectural linework in the city's central sacred precinct. Reference temple footprint approx 160m square; proposed city approx 900-1100m across. Temple should occupy about one fifth to one quarter of the main map width and remain legible. No changing it into the earlier generic three-level concept.
VIEW / MEDIUM: EXACTLY VERTICAL 90-DEGREE ORTHOGRAPHIC plan, like a beautiful professional hand-drawn masterplan / fantasy atlas / archaeological reconstruction drawing. Absolutely no angled camera, no perspective, no isometric buildings, no horizon, no visible facade elevations. Thin sepia ink building footprint lines, lightly washed sandstone roofs, open courtyard voids, staircase tread marks, palms as overhead circular crowns, small restrained shadows for heights only. Readable streets, walls, internal courtyards, service paths, public spaces; distinguish roof footprints and open ground clearly. Landscape high-resolution composition, entire city walls and outside approaches visible, moderate parchment margin. City plan dominates page; small elegant title and compact Korean legend in margin, no inset scenes.
CITY DESIGN, historically informed but fictional:
1. Amarna inspiration: a formal sacred/administrative precinct, a processional approach, court buildings and ritual service yards.
2. Palmyra inspiration: a broad colonnaded commercial/processional avenue connecting gate, market squares and sacred precinct, narrower cross streets and warehouse access.
3. Shibam inspiration: tightly grouped sun-dried mudbrick courtyard house blocks, narrow shaded alleys, irregular smaller parcels within an overall fortified city plan; do not depict modern tower blocks.
4. Petra inspiration: rainwater collection channels from the rocky uplands, cisterns, settling basins and a carefully engineered reservoir network; do not place a huge river magically in the desert.
SPECIFIC LAYOUT: central temple slightly north of map center, squared sacred precinct and clear open processional perimeter. Four short diagonal approaches meet the existing four corner stair entrances. An axial ceremonial avenue from the main southern city gate reaches a generous forecourt south of the temple, then divides around the precinct to the southwest/southeast corner entrances instead of cutting through solid temple walls. Civic palace, archive and priestly courtyard buildings to north/northeast of temple. Main bazaar and colonnaded market courts south/southeast along the avenue, two large caravanserai courtyard inns and loading yards near southern gate, paired with warehouses. Dense fine-grained residential blocks west and east of temple, with small local plazas, communal wells, courtyard gardens; varied blocks grown over time, not perfect mirror symmetry. Artisan yards, pottery kilns and storage east near an eastern service gate, separate from formal ceremonial square. A modest spring-fed/collected-water reservoir and connected geometric palm groves and irrigated plots occupy the western/southwestern edge; thin channels come from northwestern rocky catchment and connect to storage and irrigation by plausible routes. Keep water localized, no moat circling the city. A cemetery with small burial compounds outside northeastern wall, removed from water/gardens. Outer city wall follows a slightly irregular polygon enclosing all urban quarters, with gates south, west and east plus a small northern postern; towers at intervals, continuous walls interrupted ONLY by credible gateways. Road network connects all districts and gate roads continue beyond the wall into desert caravan trails. Peripheral agricultural plots outside west wall are reachable from west gate. Dunes and sparse rocky contours outside town, NOT covering inhabited streets. Rich spatial hierarchy: monumental temple alone in scale and stone quality; smaller humble dwellings, busy civic courts and infrastructure around it.
VISUAL PALETTE: cream paper ground, thin dark brown outlines, sandstone/ochre public buildings, clay beige dwellings, muted terracotta awnings, restrained teal water and desaturated olive gardens. Light wash hints differentiate districts without giant colored zoning circles. Sober professional readable level-design reference, detailed but not noisy, no ornamental fantasy compass consuming space. A small north arrow is fine.
EXACT KOREAN TITLE: "모래에 묻히기 전의 신전 도시"
SUBTITLE: "도시 복원 상상도 · 수직 평면"
Use small numbered markers 01 through 09 once each on the relevant district, with a compact legible Korean legend at bottom or right:
01 신전 성역
02 행렬 대로
03 시장과 광장
04 상단 숙소
05 주거 지구
06 공방과 창고
07 저수지와 정원
08 궁정과 사제 구역
09 성외 묘역
No other text needed. Keep labels away from temple circulation so the actual referenced shape is visible. No visible people, no game HUD, no glowing fantasy circles, no modern roads, no oblique cityscape. This should look like a coherent inhabited historical city PLAN built around the user's specific temple, not a generic radial castle town.

