# Game Design Document: Scripted Horror Events (Film Noir Space Horror)

## Overview & Art Direction
- **Game Theme**: 6DoF Deep Space Isolation, Cosmic Horror, Time Loop (Horror Jam 2026).
- **Visual Style**: Strictly **Film Noir (Pure Monochrome B&W)** adhering to `.agents/rules/film_noir_style.md`.
- **Day Progression & Horror Pacing**:
  - **Day 1: Tutorial & Foundation (สร้างความคุ้นเคย & False Sense of Security)**: สอนระบบพื้นฐาน 6DoF, การจูนคลื่น Signal Tracker, การรับภารกิจที่ Terminal และการบินกลับเข้า Airlock ปิดวันอย่างปลอดภัย
  - **Day 2: Event 0 - "The Catastrophic Rupture" (ซ่อม O2 Station แล้วระเบิด - บังคับตายเปิดลูป)**: ทำลายความมั่นใจของผู้เล่นด้วยการระเบิดของสถานี O2 บังคับชุดรั่วเหลือ 30% และตายในอวกาศเป็นศพแรก
  - **Day 3 (Loop Reset): Event 1 - "The Unsettled Corpse" (ศพตัวเองในลูปก่อนหน้าที่ไม่อยู่นิ่ง)**: ตื่นมาในยานหลังความตายแรก มีคำสั่งให้บินไปกู้ข้อมูลจากศพตนเองที่ตายใน Day 2 แล้วพบกับความสยอง
  - **Day 3+ / Deep Space: Event 2 - "Phantom Frequency 00.0 MHz" (สัญญาณวิทยุจากคลื่นที่ไม่มีในสารบบ)**: คลื่นปริศนาแทรกแซงและเสียงเคาะกระจกหมวก

---

## Day 1: Tutorial & Normal Expedition (สอนผู้เล่น & สร้างความคุ้นเคย)

### 1. Concept & Objective
ให้ผู้เล่นได้เรียนรู้และคุ้นเคยกับ Mechanics ทั้งหมดของเกมโดยยังไม่มีความสยองขวัญเกิดขึ้น สร้างความรู้สึกว่าเกมเป็น "ภารกิจกู้ภัยอวกาศทั่วไป" (False Sense of Security)
1. **Wake-up & Terminal**: ตื่นขึ้นในยาน เดินไปเซ็นสัญญารับภารกิจ Day 1 ที่ Mission Terminal
2. **Flight Controls**: ฝึกบังคับ 6DoF Zero-G, การเปิด/ปิด RCS Stabilizer และไฟฉาย Helmet Flashlight
3. **Signal Tracking**: เรียนรู้การใช้ Signal Tracker เพื่อจูนหาความถี่ของดาวเทียมเป้าหมาย (เช่น `142.8 MHz`)
4. **Mission Objective**: บินไปเก็บกู้ Data Log จากซากดาวเทียม (Hold `[LMB]`)
5. **Safe Return**: บินตามสัญญาณ Airlock-Home (`121.5 MHz`) กลับเข้ายาน กดจบวัน Day 1 อย่างปลอดภัย

---

## Day 2: Event 0 - The Catastrophic Rupture (ซ่อม O2 Station แล้วระเบิด - บังคับตายเปิดลูป)

### 1. Concept & Narrative
หลังจากผู้เล่นผ่าน Day 1 มาได้อย่างปลอดภัย วันนี้จะทำหน้าที่เป็น **"จุดหักมุม (The Inciting Incident)"** บังคับให้ผู้เล่นพบกับความตายครั้งแรก (Scripted Inevitable Death) เพื่อปลดล็อคระบบ Persistent Corpse และ Time Loop
- **หมายเหตุสำคัญ**: ถัง `Test Oxygen Tank` ในฉากเป็นทรัพยากรเอาชีวิตรอดทั่วไป (Survival Refill Pickups ให้กดเติม O2 +40% เพื่อเดินทางต่อได้) ซึ่งจะไม่ระเบิดและใช้งานได้ปกติทุกวัน
- **เป้าหมายของ Day 2** คือสถานีหลัก **`Auxiliary O2 Substation`** (Beacon `156.8 MHz`) ประจำ Sector 2
- ผู้เล่นรับภารกิจ Day 2 ให้ตามสัญญาณคลื่นวิทยุ `156.8 MHz` ไปซ่อมบำรุง `Auxiliary O2 Substation`
- เมื่อซ่อมบำรุงเสร็จสิ้น (Progress 100%) วาล์วแรงดันของสถานีย่อยเกิด Overload ส่งเสียงโลหะลั่น และ **ระเบิดฉีกขาดอย่างรุนแรง (Explosive Decompression)**
- แรงระเบิดและสะเก็ดโลหะฉีกกระชากชุดอวกาศของผู้เล่น **บังคับลดค่าความคงทนของชุด (Suit Integrity) เหลือ 15% ทันที**
- ทำให้เกิดรอยรั่วขั้นวิกฤต (`[!] SUIT LEAK: 15%`) ออกซิเจนพุ่งรั่วไหลอย่างรวดเร็วเกินกว่าจะกลับยานได้ทัน บังคับให้ผู้เล่นค่อยๆ ขาดอากาศหายใจตายในความมืด
- ศพจะลอยคว้างอยู่ข้างซากสถานี เพื่อเป็นตัวจุดชนวนให้เกิด **Event 1** ใน Day 3

### 2. Trigger Conditions
- **Day / Loop**: **Day 2** (กำหนดข้อมูลภารกิจใน Inspector ของ `DayCycleManager` ใน Scene โดยตรง ไม่ Hard-code)
- **Target Object**: `Auxiliary_O2_Substation` (สถานีย่อยเป้าหมายภารกิจ คลื่น Beacon `156.8 MHz`)
- **Action**: ขณะผู้เล่นกดค้าง `[LMB]` เพื่อซ่อมแซม `Auxiliary_O2_Substation` จนเสร็จสิ้น (Progress 100%)

### 3. Staging Sequence (ลำดับเหตุการณ์)
| ลำดับ | เหตุการณ์ | เสียง & วิชวล (Film Noir) |
|---|---|---|
| **1. ก่อนระเบิด (Telegraph)** | ขณะซ่อมแซมถึง 95% ไฟสถานีเริ่มกระพริบถี่ผิดปกติ เข็มวัดแรงดันสั่นอย่างรุนแรง | เสียงโลหะลั่น "เอี๊ยด... ปึง..." และเสียงความถี่สูงหวีดแหลมในหมวก |
| **2. จังหวะระเบิด (The Rupture)** | พอเต็ม 100% ถัง O2 ระเบิดฉีกขาด กระแทกร่างผู้เล่นลอยหมุนเคว้ง 6DoF ไปในความมืด | หน้าจอสว่างวาบขาวสนิท (Stark White Flash 0.2s) ก่อนตัดเป็น Noise Static |
| **3. ชุดฉีกขาด (Catastrophic Breach)** | ค่า Suit Integrity บน HUD **ถูกบังคับลดฮวบเหลือ 15% ทันที** ข้อความเตือนกระพริบ: `[!] SUIT LEAK: 15%` | เสียงก๊าซออกซิเจนพุ่งรั่วฟู่อย่างรุนแรงออกจากชุด (Continuous High-Pressure Hiss) |
| **4. ความตายที่หลีกเลี่ยงไม่ได้** | ออกซิเจนรั่วไหลด้วยความเร็วสูงมาก (Massive Leak Rate) แถบ O2 ไหลลงอย่างรวดเร็ว ขอบจอเริ่มเกิด Suffocation Vignette มืดลงทีละนิด | เสียงหัวใจเต้นถี่รัว 150+ BPM สลับกับเสียงหายใจหอบเฮือกสุดท้าย |
| **5. การสิ้นชีพ & กำเนิดลูป** | ผู้เล่นหมดสติและขาดอากาศหายใจ (Asphyxiation) จอมืดดับสนิท ร่างกายกลายเป็น **Persistent Corpse** ลอยอยู่ข้างสถานี | ตัดฉากกลับมาลืมตาตื่นขึ้นมาใหม่ในยานอวกาศ (Day 3 Begins) พร้อมคำสั่งให้ไปเก็บกู้ข้อมูลจากศพตัวเอง |

### 4. Technical Integration Hooks & Multi-Station Support
- **Multi-Station Support (รองรับหลาย Station ใน Day 2)**:
  - ใน Inspector ของ `DayCycleManager` สามารถใส่ภารกิจซ่อม O2 Substation กี่แห่งก็ได้ใน Day 2 (เช่น Substation Alpha, Beta)
  - ระบบตรวจจับอัตโนมัติ: Station ก่อนหน้าทั้งหมดจะซ่อมผ่านได้อย่างปลอดภัย (`OBJECTIVE SECURED (1/2) // PROCEED TO NEXT TARGET`) เพื่อหลอกให้ผู้เล่นตายใจ
  - **การระเบิด (Catastrophic Rupture) จะเกิดขึ้นที่ Station สุดท้ายเท่านั้น** (Whichever is completed last โดยตรวจจาก `allMissionsDone`)
  - มีสวิตช์ `Trigger On Last Station Only` บน `HorrorEventManager` ใน Inspector
- **Target Components**: `MissionInteractable.cs` (O2 Station), `AstronautSurvival.cs`, `AstronautHelmetHUD.cs`, `HorrorEventManager.cs`
- **Hooks**:
  - `onInteracted`: `DayCycleManager.HandleObjectiveInteracted` ตรวจสอบ `allMissionsDone` และส่งสัญญาณเข้า `HorrorEventManager.Instance.HandleObjectiveCompleted(2, mission, player, allMissionsDone)`
  - `AstronautSurvival.ForceSuitIntegrity(15f)`: เซ็ตค่า `currentSuitIntegrity = 15f` ทันที (85% leak tier)
  - `AstronautMovement6D`: สั่ง `rb.AddForce` และ `rb.AddTorque` ให้ลอยหมุนเคว้ง 6DoF
  - `AstronautHelmetHUD.TriggerNoirFlash(0.45f)`: แฟลชสว่างวาบขาวสนิทพร้อมแจ้งเตือน `[!] CATASTROPHIC BREACH // SUIT INTEGRITY 15%`

---

## Day 3: Event 1 - The Unsettled Corpse (ความสยองจากศพตัวเอง)

### 1. Concept & Narrative
**ต่อเนื่องจากความตายใน Day 2**: ผู้เล่นฟื้นขึ้นมาในยานใน Day 3 (ลูปใหม่) ได้รับ Directive ว่าภารกิจ Day 2 ล้มเหลวเนื่องจากนักบินคนก่อนเสียชีวิต ให้บินกลับไปเก็บกู้ข้อมูลภารกิจ (Collect Mission Data) จากศพของตนเองที่ลอยอยู่ข้างสถานี O2 ทว่าศพนั้นกลับมีปฏิกิริยาตอบสนองอย่างน่าขนลุก

### 2. Trigger Conditions
- **Day / Loop**: **Day 3** (หรือเมื่อบินเข้าไปกู้ข้อมูลจากศพ Day 2)
- **Phase**: Mission Exploration Phase
- **Proximity**: เมื่อผู้เล่นบินเข้าใกล้ศพในระยะ < 6 เมตร หรือขณะทำการกดค้าง `[LMB]` เพื่อกู้ข้อมูล

### 3. Staging Sequence (ลำดับเหตุการณ์)
| ลำดับ | เหตุการณ์ | เสียง & วิชวล (Film Noir) |
|---|---|---|
| **1. การตรวจจับคลื่น** | คลื่นวิทยุจับสัญญาณชีพจรเต้นช้าๆ (40 BPM) ออกมาจากหมวกศพ ทั้งที่ตายไปแล้ว | เสียง Static เบาๆ สลับเสียงคลื่นหัวใจต่ำกังวาน |
| **2. ระหว่างเก็บกู้ข้อมูล** | ขณะผู้เล่นกดค้าง `[LMB]` เพื่อ Recover Data (ความคืบหน้า 60%) หมวกของศพจะค่อยๆ **หมุนคอเอียงหันกลับมาสบตากับผู้เล่นโดยตรง** ช้าๆ แบบผิดธรรมชาติ | มีเสียงข้อต่อชุดอวกาศบิดตัวในสุญญากาศ "เอี๊ยด... แกร๊ก..." ผ่านการสั่นสะเทือน |
| **3. สัญญาณเตือน** | ค่า Heart Rate บน HUD ของผู้เล่นพุ่งสูงทะลุ 130+ BPM ทันที | ไอคอนรูปหัวใจกระพริบถี่เป็นสีขาวสลับดำ |
| **4. จุดกระตุกขวัญ** | เมื่อเก็บกู้ครบ 100% ไฟฉายบนหมวกของศพที่เคยดับอยู่ จะ **กระพริบติดสว่างวาบขึ้นมาอัดเข้าหน้ากากผู้เล่น 1 ครั้ง** พร้อมเสียงไฟช็อตสั้นๆ | ลำแสงขาวจัดตัดความมืดสนิท (Chiaroscuro Jolt) จากนั้นไฟศพจะดับลง และศพจะกลับมาแน่นิ่งลอยตามแรงเฉื่อยตามเดิม |

### 4. Technical Integration Hooks
- **Target Component**: `FallenAstronautRecovery.cs`
- **Hooks**:
  - `onHoldProgress`: ตรวจสอบเมื่อ progress เกิน 0.5f -> สั่ง Lerp หมุนกระดูกส่วนคอ (`headBone` หรือ transform ของหมวกศพ) ให้หันมายังผู้เล่น
  - `onInteracted`: สั่งให้ Flashlight ของศพกะพริบติด (`corpseFlashlight.enabled = true` เป็นเวลา 0.6 วินาที)
  - `AstronautSurvival`: สั่ง `SimulateStressHeartRate(140f)` ชั่วคราว

---

## Event 2: Phantom Frequency 00.0 MHz (สัญญาณที่ไม่มีอยู่ในบันทึก)

### 1. Concept & Narrative
ในความเวิ้งว้างของอวกาศลึกที่ไร้เสียง วิทยุสื่อสารคือสิ่งเดียวที่เชื่อมต่อผู้เล่นกับโลกภายนอก แต่จู่ๆ อุปกรณ์รับสัญญาณกลับถูกแทรกแซงด้วยคลื่นความถี่ลึกลับที่ไม่มีอยู่ในตารางภารกิจ พร้อมเสียงเคาะที่กระจกหมวกจากภายนอก

### 2. Trigger Conditions
- **Day / Loop**: กำหนดให้เกิดแบบ Scripted ใน Day 3 หรือเมื่อออกสำรวจระยะไกล
- **Trigger Zone / Distance**: เมื่อผู้เล่นลอยตัวห่างจากตัวยานแม่ (Ship) เกิน 60 เมตร ในจุดอับสายตา
- **Frequency Tune**: ผู้เล่นจูนคลื่นหาเป้าหมายภารกิจ หรือวิทยุถูก Forced Glitch จูนไปที่ `00.0 MHz`

### 3. Staging Sequence (ลำดับเหตุการณ์)
| ลำดับ | เหตุการณ์ | เสียง & วิชวล (Film Noir) |
|---|---|---|
| **1. วิทยุถูกแทรกแซง** | จู่ๆ จอ Signal Tracker มุมบนซ้ายจะกะพริบสั่น แล้วตัวเลขความถี่ถูกบังคับเลื่อนกลายเป็น `00.0 MHz // [UNKNOWN]` | เสียงคลื่น Static ซ่าแตกกระทันหัน |
| **2. Oscilloscope Anomaly** | จอแสดงรูปคลื่น Oscilloscope ไม่แสดงคลื่น Sine ปกติ แต่จะแปรสภาพเป็น **เส้นตรงยาวราบเรียบ (Flatline)** หรือคลื่นรูปคล้ายฟันเลื่อยผิดธรรมชาติ | สีขาวดำ Stark Invert สลับไปมา |
| **3. เสียงเคาะกระจกหมวก** | มีเสียงเคาะจากภายนอกกระจกหน้ากากผู้เล่น **"ก๊อก... ก๊อก... ก๊อก..."** ชัดเจน 3 ครั้ง (ทั้งที่รอบตัวในอวกาศไม่มีสิ่งใดลอยอยู่เลย) | คลื่นเสียงแรงกระทบเนื้อกระจก (Direct Visor Impact Audio) |
| **4. ข้อความปริศนา** | หน้ากาก HUD แสดงหน้าต่าง Transmission Log กลางหน้าจอขึ้นมาเอง: <br>`>> INCOMING AUDIO TRANSMISSION`<br>`"...STOP LEAVING US OUT HERE..."` | ปรากฏอยู่ 3.5 วินาที แล้วสัญญาณจะดับวูบ กลับคืนสู่ความถี่ปกติทันที |

### 4. Technical Integration Hooks
- **Target Component**: `AstronautSignalHUD.cs` & `AstronautHelmetHUD.cs`
- **Hooks**:
  - `AstronautSignalHUD.OverrideDisplay("00.0 MHz", "[UNKNOWN]", flatlineMode: true)`
  - `AstronautHelmetHUD.ShowMissionTransmission(">> INCOMING TRANSMISSION", "...STOP LEAVING US OUT HERE...", 3.5f)`
  - `AstronautAudioSystem.PlayVisorKnockSound()`

---

## Architecture Plan: `HorrorEventManager.cs`

เพื่อให้ง่ายต่อการจัดการ Event ทั้งหมดโดยไม่ผูกติดแน่นกับระบบเดิม (Decoupled Design):

```
DayCycleManager / MissionInteractable / FallenAstronautRecovery
                             │
                             ▼
                    [HorrorEventManager]
                             │
    ┌────────────────┬───────┴────────┬────────────────┐
    ▼                ▼                ▼                ▼
O2 Explosion (Day 2) Corpse Stalker (Day 3) Phantom Radio   Helmet Visor FX
(Suit 30% Leak)      (Head Turn + Strobe)   (Flatline Audio) (Blackout Strobe)
```

1. **`HorrorEventManager` (Singleton / Scene Controller)**:
   - ตรวจสอบ `DayCycleManager.Instance.CurrentDay`
   - Day 1: ไม่มี Horror Event รันระบบ Tutorial ปลอดภัย
   - Day 2: ควบคุม Event 0 (`TriggerO2StationExplosion`)
   - Day 3: ควบคุม Event 1 (`HandleCorpseRecoveryProgress`, `TriggerCorpseJolt`) และ Event 2
2. **State Management**: บันทึก One-Shot Flags ลงใน Session/Loop เพื่อให้ Event เกิดขึ้นตามลำดับเวลาที่ออกแบบไว้อย่างสมบูรณ์แบบ
