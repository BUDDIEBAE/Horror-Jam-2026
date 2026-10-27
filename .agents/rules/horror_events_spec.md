# Art & Gameplay Specification: Scripted Horror Events

## 1. Overview & Guidelines
- **Game Theme**: 6DoF Deep Space Isolation, Cosmic Horror, Time Loop (Horror Jam 2026).
- **Art Direction**: Strictly **Film Noir (Pure Monochrome B&W)**. Zero color. Follows `.agents/rules/film_noir_style.md`.
- **Chronological Flow by Days**:
  - **Day 1**: Tutorial & Onboarding (สอนการเล่นพื้นฐาน 6DoF, การจูนวิทยุ, การรับงานที่ Terminal, จบวันปลอดภัย)
  - **Day 2 (Event 0)**: "The Catastrophic Rupture" (ซ่อม O2 Station แล้วระเบิด - บังคับตายเปิดลูปแรก)
  - **Day 3 (Event 1)**: "The Unsettled Corpse" (ศพตัวเองจาก Day 2 ที่ไม่อยู่นิ่ง - ต่อเนื่องหลังฟื้นคืนชีพ)
  - **Day 3+ (Event 2)**: "Phantom Frequency 00.0 MHz" (สัญญาณวิทยุจากคลื่นที่ไม่มีในสารบบ & เสียงเคาะกระจก)

---

## 2. Day 1: Tutorial & Foundation (สอนผู้เล่น & สร้างความคุ้นเคย)

### Concept & Objectives
ให้ผู้เล่นได้เรียนรู้และคุ้นเคยกับ Mechanics ทั้งหมดของเกมโดยยังไม่มีความสยองขวัญเกิดขึ้น สร้างความรู้สึกว่าเกมเป็นภารกิจกู้ภัยอวกาศทั่วไป (False Sense of Security)
1. **Wake-up & Terminal**: ตื่นขึ้นในยาน เดินไปเซ็นสัญญารับภารกิจ Day 1 ที่ Mission Terminal
2. **Flight Controls**: ฝึกบังคับ 6DoF Zero-G, การเปิด/ปิด RCS Stabilizer และไฟฉาย Helmet Flashlight
3. **Signal Tracking**: เรียนรู้การใช้ Signal Tracker เพื่อจูนหาความถี่ของดาวเทียมเป้าหมาย (เช่น `142.8 MHz`)
4. **Mission Objective**: บินไปเก็บกู้ Data Log จากซากดาวเทียม (Hold `[LMB]`)
5. **Safe Return**: บินตามสัญญาณ Airlock-Home (`121.5 MHz`) กลับเข้ายาน กดจบวัน Day 1 อย่างปลอดภัย

---

## 3. Day 2: Event 0 - "The Catastrophic Rupture" (ซ่อม O2 Station แล้วระเบิด - บังคับตายเปิดลูป)

### Context & Narrative
หลังจากผู้เล่นผ่าน Day 1 มาได้อย่างปลอดภัย วันนี้จะทำหน้าที่เป็น **"จุดหักมุม (The Inciting Incident)"** บังคับให้ผู้เล่นพบกับความตายครั้งแรก (Scripted Inevitable Death) เพื่อปลดล็อคระบบ Persistent Corpse และ Time Loop
- **หมายเหตุสำคัญ**: ถัง `Test Oxygen Tank` ในฉากเป็นทรัพยากรเอาชีวิตรอดทั่วไป (Survival Refill Pickups ให้กดเติม O2 +40% เพื่อเดินทางต่อได้) ซึ่งจะไม่ระเบิดและใช้งานได้ปกติทุกวัน
- **เป้าหมายของ Day 2** คือสถานีหลัก **`Auxiliary O2 Substation`** (Beacon `156.8 MHz`) ประจำ Sector 2
- ผู้เล่นรับภารกิจ Day 2 ให้ตามสัญญาณคลื่นวิทยุ `156.8 MHz` ไปซ่อมบำรุง `Auxiliary O2 Substation`
- เมื่อซ่อมบำรุงเสร็จสิ้น (Progress 100%) วาล์วแรงดันของสถานีย่อยเกิด Overload ส่งเสียงโลหะลั่น และ **ระเบิดฉีกขาดอย่างรุนแรง (Explosive Decompression)**
- แรงระเบิดและสะเก็ดโลหะฉีกกระชากชุดอวกาศของผู้เล่น **บังคับลดค่าความคงทนของชุด (Suit Integrity) เหลือ 30% ทันที**
- ทำให้เกิดรอยรั่วขั้นวิกฤต (`[!] SUIT LEAK: 30%`) ออกซิเจนพุ่งรั่วไหลอย่างรวดเร็วเกินกว่าจะกลับยานได้ทัน บังคับให้ผู้เล่นค่อยๆ ขาดอากาศหายใจตายในความมืด
- ศพจะลอยคว้างอยู่ข้างซากสถานี เพื่อเป็นตัวจุดชนวนให้เกิด **Event 1** ใน Day 3

### Trigger Conditions
- **Day / Loop**: **Day 2**
- **Target Object**: `Auxiliary_O2_Substation` (สถานีย่อยเป้าหมายภารกิจ คลื่น Beacon `156.8 MHz`)
- **Action**: ขณะผู้เล่นกดค้าง `[LMB]` เพื่อซ่อมแซม `Auxiliary_O2_Substation` จนเสร็จสิ้น (Progress 100%)

### Staging Sequence (ลำดับเหตุการณ์)
1. **ก่อนระเบิด (Telegraph)**: ขณะซ่อมแซมถึง 95% ไฟสถานีเริ่มกระพริบถี่ผิดปกติ เข็มวัดแรงดันสั่นอย่างรุนแรง พร้อมเสียงโลหะลั่น "เอี๊ยด... ปึง..." และเสียงความถี่สูงหวีดแหลมในหมวก
2. **จังหวะระเบิด (The Rupture)**: พอเต็ม 100% ถัง O2 ระเบิดฉีกขาด กระแทกร่างผู้เล่นลอยหมุนเคว้ง 6DoF ไปในความมืด หน้าจอสว่างวาบขาวสนิท (Stark White Flash 0.2s) ก่อนตัดเป็น Noise Static
3. **ชุดฉีกขาด (Catastrophic Breach)**: ค่า Suit Integrity บน HUD **ถูกบังคับลดฮวบเหลือ 30% ทันที** ข้อความเตือนกระพริบ: `[!] SUIT LEAK: 30%` พร้อมเสียงก๊าซออกซิเจนพุ่งรั่วฟู่อย่างรุนแรงออกจากชุด (Continuous High-Pressure Hiss)
4. **ความตายที่หลีกเลี่ยงไม่ได้**: ออกซิเจนรั่วไหลด้วยความเร็วสูงมาก แถบ O2 ไหลลงอย่างรวดเร็ว ขอบจอเริ่มเกิด Suffocation Vignette มืดลงทีละนิด พร้อมเสียงหัวใจเต้นถี่รัว 150+ BPM สลับกับเสียงหายใจหอบเฮือกสุดท้าย
5. **การสิ้นชีพ & กำเนิดลูป**: ผู้เล่นหมดสติและขาดอากาศหายใจ (Asphyxiation) จอมืดดับสนิท ร่างกายกลายเป็น **Persistent Corpse** ลอยอยู่ข้างสถานี ตัดฉากกลับมาลืมตาตื่นขึ้นมาใหม่ในยานอวกาศ (Day 3 Begins) พร้อมคำสั่งให้ไปเก็บกู้ข้อมูลจากศพตัวเอง

### Code Integration Hooks & Multi-Station Support
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

## 4. Day 3: Event 1 - "The Unsettled Corpse" (ศพตัวเองในลูปก่อนหน้าที่ไม่อยู่นิ่ง)

### Context & Narrative
**ต่อเนื่องจากความตายใน Day 2**: ผู้เล่นฟื้นขึ้นมาในยานใน Day 3 ได้รับ Directive ว่าภารกิจ Day 2 ล้มเหลวเนื่องจากนักบินคนก่อนเสียชีวิต ให้บินกลับไปเก็บกู้ข้อมูลภารกิจ (Collect Mission Data) จากศพของตนเองที่ลอยอยู่ข้างสถานี O2 ทว่าศพนั้นกลับมีปฏิกิริยาตอบสนองอย่างน่าขนลุก

### Trigger Conditions
- **Day / Loop**: **Day 3** (หรือเมื่อบินเข้าไปกู้ข้อมูลจากศพ Day 2)
- **Phase**: Mission Exploration Phase
- **Proximity / Action**: เข้าใกล้ศพ < 6 เมตร หรือขณะทำการกดค้าง `[LMB]` กู้ข้อมูล

### Staging Sequence (ลำดับเหตุการณ์)
1. **Pulse Detection**: จูนคลื่นวิทยุใกล้ศพ ตรวจจับเสียงคลื่นหัวใจเต้นช้าๆ (40 BPM) ดังออกมาจากหมวกศพ ทั้งที่ตายไปแล้ว
2. **Head Turn Anomaly**: ระหว่างที่ผู้เล่นกด `[LMB]` เพื่อกู้ข้อมูล (เมื่อ Progress ผ่าน 50-60%) หมวกของศพจะค่อยๆ **หมุนคอหันกลับมาสบตากับหน้ากากของผู้เล่นโดยตรง** ช้าๆ ผิดธรรมชาติ พร้อมเสียงข้อต่อชุดอวกาศบิดตัว
3. **Heart Rate Spike**: อัตราการเต้นของหัวใจผู้เล่นพุ่งสูง 130+ BPM ทันที ไอคอนหัวใจบน HUD กระพริบถี่
4. **Flashlight Jolt**: เมื่อกู้ข้อมูลครบ 100% ไฟฉายบนหมวกของศพที่เคยดับอยู่ จะ **กะพริบติดสว่างวาบอัดเข้าหน้ากากผู้เล่น 1 ครั้ง** (Chiaroscuro Flash) พร้อมเสียงไฟช็อตสั้นๆ ก่อนดับลง และศพจะกลับมาแน่นิ่งลอยตามแรงเฉื่อยตามเดิม

### Code Integration Hooks
- **Target**: `Assets/Scripts/Interaction/FallenAstronautRecovery.cs`
- **Actions**:
  - `onHoldProgress`: Lerp หมุนทิศทางหัว/หมวกของศพ (`headBone` หรือ `neckTransform`) มาที่ Player Camera
  - `onInteracted`: สั่งไฟฉายของศพกะพริบติดชั่วคราว (`corpseFlashlight.enabled = true` ~0.6s)
  - `AstronautSurvival`: สั่ง `SimulateStressHeartRate(140f)`

---

## 5. Event 2: "Phantom Frequency 00.0 MHz" (สัญญาณวิทยุจากคลื่นที่ไม่มีในสารบบ)

### Context & Narrative
ในความมืดและความเงียบของอวกาศลึก วิทยุสื่อสารคือสิ่งเดียวที่บอกตำแหน่ง แต่จู่ๆ ตัวรับสัญญาณกลับโดนแทรกแซงด้วยคลื่นความถี่ลึกลับที่ไม่มีในสารบบ

### Trigger Conditions
- **Condition**: เกิดขึ้นแบบ Scripted One-Shot ใน Day 3 หรือเมื่อออกสำรวจระยะไกล (> 60m)
- **Location**: ลอยตัวห่างจากตัวยานแม่ (Ship) เกิน 60 เมตร ในจุดอับสายตา
- **Action**: เมื่อผู้เล่นกำลังจูนวิทยุหาเป้าหมายภารกิจ

### Staging Sequence (ลำดับเหตุการณ์)
1. **Radio Hijack**: จอ [AstronautSignalHUD](file:///c:/Oat/Personal/Horror-Jam-2026/Assets/Scripts/UI/AstronautSignalHUD.cs) มุมบนซ้ายมีอาการกระตุก สัญญาณถูกบังคับจูนไปที่ `00.0 MHz // [UNKNOWN]`
2. **Oscilloscope Flatline**: กราฟ Oscilloscope ไม่แสดงรูป Sine wave ปกติ แต่กลายเป็น **เส้นตรงยาวราบเรียบ (Flatline)** หรือคลื่นฟันเลื่อยผิดธรรมชาติ สลับ Stark Monochrome Inversion
3. **Visor Knocking**: มีเสียงเคาะจากภายนอกกระจกหน้ากากผู้เล่น **"ก๊อก... ก๊อก... ก๊อก..."** 3 ครั้งชัดเจน ทั้งที่รอบตัวในอวกาศไม่มีสิ่งใดลอยอยู่
4. **Audio Transmission Popup**: หน้าจอ HUD เด้ง Transmission Log กลางหน้าจอ:  
   `>> INCOMING AUDIO TRANSMISSION`  
   `"...STOP LEAVING US OUT HERE..."`  
   (ปรากฏ 3.5 วินาที แล้วสัญญาณจะตัดวูบ กลับคืนสู่ความถี่ปกติ)

### Code Integration Hooks
- **Target**: `Assets/Scripts/UI/AstronautSignalHUD.cs` & `Assets/Scripts/UI/AstronautHelmetHUD.cs`
- **Actions**:
  - `AstronautSignalHUD.OverrideDisplay("00.0 MHz", "[UNKNOWN]", flatlineMode: true)`
  - `AstronautHelmetHUD.ShowMissionTransmission(">> INCOMING TRANSMISSION", "...STOP LEAVING US OUT HERE...", 3.5f)`
  - `AstronautAudioSystem.PlayVisorKnockSound()`

---

## 6. Architecture: `HorrorEventManager.cs`

สร้าง Script กลางชื่อ `HorrorEventManager.cs` ใน `Assets/Scripts/Gameplay/` เพื่อควบคุม Event ทั้งหมด:
- ตรวจสอบ `DayCycleManager.Instance.CurrentDay`
- Day 1: ไม่มี Horror Event รันระบบ Tutorial ปลอดภัย
- Day 2: ควบคุม Event 0 (`TriggerO2StationExplosion`)
- Day 3: ควบคุม Event 1 (`HandleCorpseRecoveryProgress`, `TriggerCorpseJolt`) และ Event 2
- จัดการ Flag เพื่อให้ Event เกิดขึ้นตามลำดับเวลาที่ออกแบบไว้
