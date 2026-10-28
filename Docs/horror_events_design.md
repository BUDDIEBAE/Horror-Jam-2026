# Game Design Document: Scripted Horror Events (Film Noir Space Horror)

## Overview & Art Direction
- **Game Theme**: 6DoF Deep Space Isolation, Cosmic Horror, Time Loop (Horror Jam 2026).
- **Visual Style**: Strictly **Film Noir (Pure Monochrome B&W)** adhering to `.agents/rules/film_noir_style.md`.
- **Day Progression & Horror Pacing**:
  - **Day 1: Tutorial & Foundation (สร้างความคุ้นเคย & False Sense of Security)**: สอนระบบพื้นฐาน 6DoF, การจูนคลื่น Signal Tracker, การรับภารกิจที่ Terminal และการบินกลับเข้า Airlock ปิดวันอย่างปลอดภัย
  - **Day 2: Event 0 - "The Catastrophic Rupture" (ซ่อม O2 Station แล้วระเบิด - บังคับตายเปิดลูป)**: ทำลายความมั่นใจของผู้เล่นด้วยการระเบิดของสถานี O2 บังคับชุดรั่วเหลือ 15% และตายในอวกาศเป็นศพแรก
  - **Day 2 (Loop Reset): Event 1 - "The Unsettled Corpse" (ศพตัวเองในลูปก่อนหน้าที่ไม่อยู่นิ่ง)**: ตื่นมาในยานหลังความตายแรก (ยังคงเป็น Day 2 ลูปเดิม) มีคำสั่งให้บินไปกู้ข้อมูลจากศพตนเองที่ตายจาก Event 0 แล้วพบกับความสยอง เมื่อกู้ข้อมูลเสร็จและกลับมานอนที่ยาน ถึงจะผ่านเข้าสู่ Day 3
  - **Day 3: Event 2 - "Luminous Anomalies & Sanity Corruption" (ลูกบอลแสงลอยคว้าง & ระบบ Sanity)**: ปรากฏลูกบอลแสงกระจัดกระจายในอวกาศ ผู้เล่นมีหน้าที่บินไป Scan ดวงไฟเหล่านั้น ซึ่งจะเพิ่มค่า Sanity ที่นำไปใช้ปลดล็อกความหลอนขั้นต่อไป

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
- ศพจะลอยคว้างอยู่ข้างซากสถานี เพื่อเป็นตัวจุดชนวนให้เกิด **Event 1** ใน Day 2 (Loop Reset)

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
| **5. การสิ้นชีพ & กำเนิดลูป** | ผู้เล่นหมดสติและขาดอากาศหายใจ (Asphyxiation) จอมืดดับสนิท ร่างกายกลายเป็น **Persistent Corpse** ลอยอยู่ข้างสถานี | ตัดฉากกลับมาลืมตาตื่นขึ้นมาใหม่ในยานอวกาศ (Day 2 Loop Retry) พร้อมคำสั่งให้ไปเก็บกู้ข้อมูลจากศพตัวเอง |

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

## Day 2 (Loop Reset): Event 1 - The Unsettled Corpse (ความสยองจากศพตัวเอง)

### 1. Concept & Narrative
**ต่อเนื่องจากความตายใน Day 2 (First Attempt)**: เมื่อผู้เล่นขาดอากาศหายใจ ระบบ Time Loop จะดึงผู้เล่นฟื้นขึ้นมาในยานใน **Day 2 เดิม (Loop Retry)** มี Directive ให้บินกลับไปเก็บกู้ข้อมูลภารกิจ (Collect Mission Data) จากศพของตนเองที่ลอยอยู่ข้างซากสถานี O2 เพื่อให้สามารถเคลียร์ภารกิจ Day 2 และปลดล็อคการกลับยานได้ ทว่าศพนั้นกลับมีปฏิกิริยาตอบสนองอย่างน่าขนลุก

### 2. Trigger Conditions
- **Day / Loop**: **Day 2 (Loop Reset)**
- **Target Corpse**: **ศพของผู้เล่นที่เกิดจากการระเบิดของ O2 Station ใน Day 2 (Event 0) เท่านั้น (Fixed Event ไม่ใช่ทุกศพ)**
- **Phase**: Mission Exploration Phase
- **Proximity**: เมื่อผู้เล่นบินเข้าใกล้ศพในระยะ < 6 เมตร หรือขณะทำการกดค้าง `[LMB]` เพื่อกู้ข้อมูล

### 3. Staging Sequence (ลำดับเหตุการณ์)
| ลำดับ | เหตุการณ์ | เสียง & วิชวล (Film Noir) |
|---|---|---|
| **1. การตรวจจับคลื่น** | คลื่นวิทยุจับสัญญาณชีพจรเต้นช้าๆ (40 BPM) ออกมาจากหมวกศพ ทั้งที่ตายไปแล้ว | เสียง Static เบาๆ สลับเสียงคลื่นหัวใจต่ำกังวาน |
| **2. ระหว่างเก็บกู้ข้อมูล** | ขณะผู้เล่นกดค้าง `[LMB]` เพื่อ Recover Data (ความคืบหน้า 50%+) หมวกของศพจะค่อยๆ **หมุนคอเอียงหันกลับมาสบตากับผู้เล่นโดยตรง** ช้าๆ แบบผิดธรรมชาติ | มีเสียงข้อต่อชุดอวกาศบิดตัวในสุญญากาศ "เอี๊ยด... แกร๊ก..." ผ่านการสั่นสะเทือน |
| **3. สัญญาณเตือน** | ค่า Heart Rate บน HUD ของผู้เล่นพุ่งสูงทะลุ 135+ BPM ทันที พร้อมเสียงสูดหายใจหอบตกใจในหมวก | ไอคอนรูปหัวใจกระพริบถี่เป็นสีขาวสลับดำ |
| **4. จุดกระตุกขวัญ (The Jolt)** | เมื่อเก็บกู้ครบ 100% เกิด Noir Visor Flash + Glitch + แรงกระตุก Trauma ที่กล้อง และไฟฉายบนหมวกของศพจะกะพริบจ้าสะท้อนเงามืดตัดดำขาว (Chiaroscuro) | เสียงไฟฟ้าลั่นเปรี๊ยะ (Electrical Snap) |
| **5. การตัดสัญญาณวิทยุ** | สัญญาณ Beacon `128.0 MHz` ของศพถูกตัดและทำลายทิ้งทันที หายไปจากเครื่องรับวิทยุอย่างถาวร วิทยุจูนกลับ Airlock `121.5 MHz` | ข้อความแจ้งเตือน `TELEMETRY RECOVERED // RETURN TO AIRLOCK` |

### 4. Technical Integration Hooks
- **Target Component**: `FallenAstronautRecovery.cs`
- **Hooks**:
  - `onHoldProgress`: ตรวจสอบเมื่อ progress เกิน 0.5f -> สั่ง Slerp หมุนกระดูกส่วนคอและศีรษะ (`spine.005` / `spine.004`) พร้อม Micro-jitter ให้หันมายังผู้เล่น
  - `TriggerShockSpike`: สั่ง `AstronautSurvival.TriggerHeartSpike(55f)` และ `AstronautAudioSystem.TriggerWakeUpGasp()`
  - `HandleDataCollected`: สั่งกล้องสั่น `AstronautCameraRig.AddTrauma(0.55f)`, หน้ากากกะพริบ `hud.TriggerNoirFlash(0.65f)`, Visor CRT Glitch `hud.TriggerVisorGlitch(0.40f)`, ไฟฉายศพกะพริบเงาชัด และเรียก `SilenceAndDestroyBeacon()` ตัดสัญญาณวิทยุออกจากระบบ

---

## Event 2: Luminous Anomalies & Sanity Corruption (ดวงไฟปริศนา & ระบบ Sanity)

### 1. Concept & Narrative
ใน **Day 3** ท่ามกลางความมืดมิดของอวกาศลึก จู่ๆ ก็มี **"ลูกบอลแสงปริศนา" (Luminous Anomaly Orbs)** ปรากฏขึ้นกระจัดกระจายลอยเคว้งอยู่ในเซกเตอร์อย่างไร้ที่มา แสงสีขาวสว่างนวลตัดกับความมืดมิดของอวกาศ (Stark Chiaroscuro) 
ศูนย์บัญชาการมอบหมายให้ผู้เล่นลอยตัวเข้าไปทำการ **Scan** เพื่อเก็บข้อมูลสเปกตรัมของดวงไฟเหล่านี้ ทว่าทุกครั้งที่ผู้เล่นสแกน รังสีและคลื่นลึกลับจะค่อยๆ ซึมลึกเข้าสู่ระบบประสาท ส่งผลให้ค่า **Sanity (สติสัมปชัญญะ)** เพิ่มระดับขึ้นเรื่อยๆ ซึ่งจะนำไปสู่ความผิดปกติทางจิตและภาพหลอนในระยะต่อไป

### 2. Trigger Conditions & Availability
- **Day / Loop**: ปรากฏขึ้น **เฉพาะใน Day 3** เท่านั้น (Day 1 และ Day 2 วัตถุเหล่านี้จะซ่อนตัวอยู่ `SetActive(false)`)
- **Interaction Method**: สแกนด้วยการลอยเข้าไปใกล้แล้ว **Hold [LMB] (2.0 วินาที)** ผ่าน `MissionInteractable`
- **Radio Frequency (Optional)**: สามารถปล่อยคลื่นอ่อนๆ เช่น `166.6 MHz // [ANOMALY]` เพื่อให้นำทางหาได้ด้วยวิทยุ

### 3. Sanity System (ค่าความคุ้มคลั่ง / สติสัมปชัญญะ)
- **ตัวแปรหลัก**: `int sanity` บน `AstronautSurvival.cs`
- **กลไกการเพิ่มค่า**:
  - เริ่มต้นที่ `sanity = 0`
  - ทุกครั้งที่ Scan ลูกบอลแสงสำเร็จ 1 ลูก: **`sanity += 1`** (พร้อมยิง Event `OnSanityChanged(sanity)`)
  - ค่า Sanity นี้จะถูกนำไปใช้เป็น Threshold สำหรับทริกเกอร์ Element ความ Horror ในอนาคต (เช่น เสียงกระซิบในหมวก, ภาพเงาหลอนที่หางตา, การกะพริบของไฟฉาย, ความผิดปกติของหน้าปัด HUD)

### 4. Staging Sequence (ลำดับเหตุการณ์ขณะ Scan)
| ลำดับ | เหตุการณ์ | เสียง & วิชวล (Film Noir) |
|---|---|---|
| **1. ค้นพบดวงไฟ** | ลูกบอลแสงสีขาวลอยนิ่ง กระเพื่อมแผ่วเบาเหมือนกำลังหายใจ (Breathing Glow) ส่องเงาแข็งตัดกับเศษซากรอบตัว | เสียงฮัมความถี่สูงสะท้อนแผ่วเบาในสุญญากาศ (Ethereal Resonance) |
| **2. ทำการสแกน (Hold [LMB])** | เมื่อเริ่มสแกน (0% - 100%) แสงของดวงไฟจะค่อยๆ เร่งความสว่างจ้าขึ้นเรื่อยๆ พร้อมความถี่เสียงที่สูงและแหลมขึ้น | เสียงกักเก็บพลังงานคล้ายหลอดสุญญากาศร้อนจัด |
| **3. สแกนสำเร็จ (The Implosion)** | ดวงไฟกะพริบจ้า 1 ครั้ง (Stark Flash) ก่อนจะยุบตัวสลายวูบลง (Implosion) ดับหายไปอย่างถาวร | เสียงแก้วแตกสะท้อนเบาๆ ในหมวก (Resonant Pop) |
| **4. ผลกระทบต่อผู้เล่น** | - ค่า `Sanity` เพิ่มขึ้น +1 <br>- หน้ากากเกิด CRT Visor Glitch สั้นๆ (0.35s)<br>- ชีพจรพุ่งกระฉูดทันที (Heart Rate Spike +30 BPM) | เสียงสูดหายใจหอบของผู้เล่นในหน้ากาก |

### 5. Technical Architecture Hooks
- **Component**: `LuminousAnomaly.cs` (ควบคุม Visual, การลอยตัว, Point Light, เสียงฮัม และ Day 3 Lifecycle)
- **Player Component**: `AstronautSurvival.cs` เพิ่มตัวแปร `public int Sanity { get; }` และฟังก์ชัน `IncreaseSanity(int amount = 1)`
- **Interaction Hook**: ใช้ `MissionInteractable` (Type = `Custom`, HoldDuration = `2.0f`, RequiresActiveMission = `true`)
- **Horror Escalation Hook**: `HorrorEventManager.cs` รับฟังสัญญาณ `OnSanityChanged` เพื่อเตรียมปลดล็อกความสยองขั้นต่อไปตามระดับ Sanity

---

## Architecture Plan: `HorrorEventManager.cs`

เพื่อให้ง่ายต่อการจัดการ Event ทั้งหมดโดยไม่ผูกติดแน่นกับระบบเดิม (Decoupled Design):

```
DayCycleManager / MissionInteractable / FallenAstronautRecovery / LuminousAnomaly
                                    │
                                    ▼
                          [HorrorEventManager]
                                    │
    ┌────────────────┬──────────────┴───────────────┬────────────────┐
    ▼                ▼                              ▼                ▼
O2 Explosion (Day 2) Corpse Stalker (Day 2 Loop)  Luminous Orbs   Sanity Distortion
(Suit 15% Leak)      (Head Turn + Strobe)         (Day 3 Scanning) (Whispers & Glitches)
```

1. **`HorrorEventManager` (Singleton / Scene Controller)**:
   - ตรวจสอบ `DayCycleManager.Instance.CurrentDay` และสถานะ `hasCorpseToRecover`
   - Day 1: ไม่มี Horror Event รันระบบ Tutorial ปลอดภัย
   - Day 2 (First Attempt): ควบคุม Event 0 (`TriggerO2StationRupture`)
   - Day 2 (Loop Reset): ควบคุม Event 1 (`HandleCorpseRecoveryProgress`, `TriggerCorpseJolt`) เมื่อกู้ศพตัวเอง
   - Day 3: ควบคุม Event 2 (เปิดการทำงานของ **Luminous Anomalies** และประมวลผล **Sanity System**)
2. **State Management**: บันทึก One-Shot Flags และค่า Sanity เพื่อให้ Event เกิดขึ้นตามลำดับเวลาที่ออกแบบไว้อย่างสมบูรณ์แบบ
