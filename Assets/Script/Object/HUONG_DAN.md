# Puzzle Tool — Hướng Dẫn Nhanh


## Quy trình

```
Author scene  →  Test Play  →  Export JSON  →  LibGDX đọc
```

Menu **Tools → Puzzle**:

- **Level Config** — metadata, win/lose, strings table
- **Level Exporter** — xuất JSON
- **Level Importer** — load lại từ JSON


---


## Components

- **B_InteractableObject** — 1 object có thể tương tác
- **B_StaticObject** — background, vật trang trí
- **B_InteractableGroup** — N object giống nhau, hoạt động **song song**
- **B_InteractableQueue** — N object xếp hàng, **tuần tự**
- **B_DropZone** — vùng nhận drop
- **B_SpineSkinSet** — combine nhiều Spine skin
- **B_LevelConfig** — metadata level (1 cái duy nhất / scene)
- **B_LevelOutcomeUI** — UI Win/Lose + nút Replay


---


## State machine

Mỗi `Object Data` chứa list **State**. Mỗi state có:

- **State Id** — tên duy nhất trong object
- **Trigger** — cử chỉ player phải làm
  - `TAP` — chạm
  - `DRAG` — kéo thả
  - `SWIPE_UP` / `SWIPE_DOWN` / `SWIPE_LEFT` / `SWIPE_RIGHT`
  - `REQUIREMENT_MET` — tự fire khi requirements thỏa
  - `NONE` — không tự fire (chỉ khi bị `ActivateState` chain)
- **Required Zone Id** — (chỉ khi trigger = DRAG) zone bắt buộc
- **Requirements** — AND list, state khác phải done / chưa done
- **Actions** — side-effect chạy khi activate
- **Sprite / Spine Anim / SFX** — visual + audio khi activate
- **Success / Fail / Hint Key** — string key cho message
- **Repeatable** — có thể fire lại không


---


## Action types

- **Wait** — đợi N giây
- **MoveTo** — tween đến target
- **Disappear** — ẩn / destroy (fade hoặc instant)
- **Appear** — hiện (fade hoặc instant)
- **DoAnimation** — chạy spine anim
- **PlaySFX** — phát AudioClip
- **ActivateState** — fire state khác
  - **Chain Guards** — list requirement; nếu **tất cả** thỏa → **skip** chain
- **AdvanceQueue** — phục vụ head của một Queue
- **SkinChange** — đổi Spine skin (Add / Remove / Toggle)


---


## Strings table (đa ngôn ngữ)

Trong **Level Config → Level Strings**, mỗi row có `key | en | vn`.

- **Collect Missing Keys** — scan scene, tự thêm key
- **Auto-fill EN from VN** — dịch hàng loạt qua Google
- **→EN** trên row — dịch một dòng

Runtime:
```csharp
B_LevelConfig.CurrentLanguage = "vn"; // hoặc "en"
string text = B_LevelConfig.Translate("msg_key");
```


---


## Win / Lose

Trong **Level Config → Outcome Conditions**:

- **Win** = **TẤT CẢ** win conditions đều thỏa (AND)
- **Lose** = **MỘT** lose condition thỏa là đủ (OR)

Mỗi LevelCondition gồm: type (`StateActivated` / `StateNotActivated`),
object id, state id.

UI: thêm `B_LevelOutcomeUI` lên Canvas/Panel, wire 3 ref:
- **Panel Root** — panel sẽ active khi level kết thúc
- **Outcome Text** — TMP_Text hiển thị "Win" / "Lose"
- **Replay Button** — nút reload scene

Sau khi level kết thúc, mọi tương tác bị **block** cho đến khi Replay.


---


## Export

Settings:

- **Asset Root** — prefix bị strip (vd `Assets/LevelAssets/`)
- **Output Asset Prefix** — prefix prepend cho LibGDX (vd `assets/levels/`)
- **Output Dir** — folder xuất (vd `Assets/LEVEL_EXPORTED/`)

Output: `outputDir/<levelId>/level.json` + `strings.json` + assets.


---


# Use Cases


## 1. Tap để chạy animation

Object `cat`, một state:

- Trigger: `TAP`
- Actions: `DoAnimation(wave_tail, 1.5s)` → `PlaySFX(meow)`
- Repeatable: true (cho phép tap lại)


## 2. Kéo coin lên ăn xin

`B_DropZone` "beggar_zone" trên người ăn xin (cùng GameObject).

Object `coin`, một state:

- Trigger: `DRAG`, Required Zone: `beggar_zone`
- Actions:
  - `Disappear` (destroy)
  - `ActivateState` → beggar.received

State `beggar.received` (trigger NONE):

- Actions: `DoAnimation(thank_you)` + `PlaySFX`


## 3. Combo kick / punch / run, fallback về stance

State `stance`: trigger NONE, repeatable, anim đứng yên.

Mỗi state `kick` / `punch` / `run`:

- Trigger: TAP (hoặc gì đó)
- Actions:
  - `DoAnimation` của action đó
  - `ActivateState → self.stance` với **Chain Guards** = 2 state **còn lại**
    - kick → guards: [punch done, run done]
    - punch → guards: [kick done, run done]
    - run → guards: [kick done, punch done]

Khi 2 state còn lại đã done, cái cuối skip chain → giữ pose, không về stance.


## 4. Hàng ăn xin (Queue)

`B_InteractableQueue` "BeggarLine":

- Members: 6 GameObject Spine
- Slots: 6 empty Transform anchor
- State `served`: `DoAnimation(receive)` → `Disappear(destroy)`

`B_DropZone` standalone tại Slot_0, zone id = `beggar_line`.

Money interactable, DRAG state:

- Required Zone: `beggar_line`
- Actions: `Disappear` → `AdvanceQueue(BeggarLine, "served")`


## 5. Cô gái Spine với 3 skin

Trên cô gái GameObject (đã có SkeletonAnimation):

- Add `B_SpineSkinSet`
- Initial Skins: `[Toc, Ao, Tui]` → cả 3 hiện cùng lúc

Object `scissors`, DRAG state:

- Required Zone: `girl_hair`
- Actions: `Disappear` → `SkinChange(girl, Remove, "Toc")`

Lặp với áo (`Ao`) và túi (`Tui`).

Đổi op thành `Toggle` nếu muốn mặc lại được.


### 5b. Gắn vật theo xương Spine (AttachToBone) — "xô nước dính tay người"

Kéo `bucket` thả vào tay người (drop zone trên tay), rồi xô đi theo tay khi
người cử động:

Object `bucket`, DRAG state (Required Zone = `man_hand`):
- Action: `AttachToBone`
  - **Bone Source**: object người (có SkeletonAnimation)
  - **Bone Name**: chọn xương từ dropdown (vd `hand_R`) — sau khi gán Bone Source
  - **Keep Offset**: bật để xô giữ đúng vị trí lúc thả (mặc định bật)
  - **Subject**: để trống = chính `bucket`

Khi muốn thả xô ra (vd người đặt xuống): dùng action `DetachFromBone`
(Subject để trống = self). Xô về parent cũ, đứng yên tại chỗ.

> Lưu ý: dùng Spine `BoneFollower` ở dưới — runtime tạo anchor tạm, không
> ghi vào JSON dạng object. Action thì có round-trip. LibGDX cần tự follow
> xương (Bone.getWorldX/Y + worldRotationX).


## 6. Object tự xuất hiện khi đủ điều kiện

State `flower.bloomed`:

- Trigger: `REQUIREMENT_MET`
- Requirements:
  - water.poured → done
  - fertilizer.applied → done
- Actions: `DoAnimation(bloom)`


### 6b. Mốc đếm (Required Count) — "cho ăn đủ N món thì lớn lên"

`Required Count` (trên mỗi State):
- `0` (mặc định) = phải đủ **TẤT CẢ** Requirements (như cũ).
- `> 0` = fire khi có **ít nhất** N Requirements thỏa.

Ví dụ: ông `man` có drop zone, nhiều món ăn kéo thả vào (mỗi món 1 state
`eaten` trigger `DRAG`). Trên `man` thêm các state `REQUIREMENT_MET`,
cùng 1 list Requirements (tất cả món `eaten`) nhưng `Required Count` tăng dần:

- `grow_1`: Required Count = 3 → `ScaleTo(1.3)`
- `grow_2`: Required Count = 6 → `ScaleTo(1.6)`

Ăn tới món thứ 3 → lớn lần 1; món thứ 6 → lớn lần 2. Mỗi mốc fire 1 lần.


## 7. Win / Lose UI + Replay

Trong scene tạo:

- Canvas → Panel "OutcomePanel"
- Trong Panel: TMP_Text + Button "Replay"
- Add `B_LevelOutcomeUI` lên Panel, wire 3 ref

Trong Level Config: set Win Conditions và Lose Conditions.

Khi player đáp ứng → panel hiện. Click Replay → reload scene.


## 8. Reset state giữa các lần chơi

Tool **không** có "Reset" action. Dùng nút Replay để reload scene → toàn
bộ `isDone` reset.


---


# Troubleshooting


## State không fire

Check theo thứ tự:

1. Trigger có khớp gesture không?
2. Required Zone Id (nếu DRAG)?
3. Tất cả requirement đã thỏa?
4. `Is Done` chưa bật (nếu chưa Repeatable)?
5. Sort Order — có object nào đang shadow không?


## Win / Lose tự fire sai thời điểm

- Win = ALL conditions met. Nếu fire sớm: 1 condition đang true vô tình
  từ đầu. Mở Level Config → Win Conditions, kiểm tra từng row.
- Lose = ANY conditions met. Hay gặp: condition `StateNotActivated`
  trên state chưa done → true ngay từ đầu. Đổi sang `StateActivated`
  của state "fail" cụ thể (vd `wrong_button.pressed`).


## Reactive state không tự chạy

`REQUIREMENT_MET` chỉ được re-evaluate sau khi action lock về 0
(state khác chạy xong). Nếu condition đã thỏa từ đầu level mà không
state nào fire trước đó, gọi từ Start():

```csharp
B_InteractableObject.CheckReactiveStatesOnce();
```


## Audio im

Nếu test scene trực tiếp (không qua bootstrap), `B_AudioManager` chưa
load. Tool fallback `AudioSource.PlayClipAtPoint` — vẫn nghe được, chỉ
không qua mixer.


## Object bị shadow / không tap được

Sort Order quyết định ai thắng khi overlap. Sprite mode đọc
`SpriteRenderer.sortingOrder`, Spine mode đọc `MeshRenderer.sortingOrder`.
Object có sort cao hơn được pick.


## Drop zone xuất hiện không đúng vùng

- Cùng GameObject với interactable → dùng chung collider của object đó.
- GameObject riêng → có collider riêng, vị trí riêng.

Đổi cách bố trí trong scene tùy nhu cầu.


## Spine chỉ chọn được 1 skin

Spine "Initial Skin" chỉ chọn 1. Để bật nhiều skin cùng lúc, dùng
`B_SpineSkinSet`.


---


# Cheat Sheet

- **Tap đơn giản** → TAP + DoAnimation
- **Kéo thả** → DRAG + Required Zone Id
- **Vuốt** → SWIPE_UP / DOWN / LEFT / RIGHT
- **Tự fire khi đủ ĐK** → REQUIREMENT_MET + Requirements
- **Chuỗi state** → ActivateState
- **Chain có ĐK** → ActivateState + Chain Guards
- **Nhóm song song** → B_InteractableGroup
- **Hàng tuần tự** → B_InteractableQueue
- **Spine multi-skin** → B_SpineSkinSet + SkinChange
- **Đa ngôn ngữ** → string key + Level Strings
- **Win/Lose** → Level Config + B_LevelOutcomeUI
- **Hint** → hintMessageKey + B_HintManager.RequestHint()
- **SFX** → PlaySFX action / State SFX field


---


# FAQ

**Cần biết code không?**
Không. Toàn bộ logic author trong inspector.

**Sau khi export?**
Copy folder `lv*/` (chứa level.json + strings.json + assets) sang
LibGDX project.

**Preview không vào Play?**
Visual hiện trong Scene view (Sprite + Spine preview, Drop Zone gizmo).
Action chỉ chạy ở Play mode.

**Đổi Object Id rồi reference cũ?**
Mở inspector hoặc Level Config → Scene Objects, sửa id. Reference cũ
hiện ⚠ cam trong dropdown.

**Nhiều B_LevelConfig trong scene?**
Không nên — singleton. Đặt 1 cái duy nhất.
