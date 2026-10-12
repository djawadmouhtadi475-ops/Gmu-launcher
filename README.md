# Gmu Launcher
لانشر Minecraft احترافي — WPF (C# / .NET 8) — **© 2026 Gmu Studios. All Rights Reserved.**

## البناء من GitHub (بدون تثبيت أي شيء على جهازك)
1. أنشئ مستودعاً جديداً على GitHub، ثم ارفع محتويات هذا المجلد كما هي (مع المجلد المخفي `.github`).
2. افتح تبويب **Actions** — سيبدأ البناء تلقائياً عند أول `push` (أو اضغط **Run workflow**).
3. عند انتهاء البناء (علامة ✅) افتح العملية ← قسم **Artifacts** ← نزّل `GmuLauncher-win-x64`.
4. فك الضغط وشغّل `GmuLauncher.exe` (ملف واحد، لا يحتاج تثبيت .NET).

### إصدار رسمي (Release)
```bash
git tag v1.0.0
git push origin v1.0.0
```
سيُنشئ الـ workflow صفحة Release فيها ملف ZIP جاهز للتحميل.

## البناء محلياً
تحتاج Windows + .NET 8 SDK:
```bash
dotnet publish GmuLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## ما هو مُنجَز
- شاشة Splash + تسجيل دخول (Microsoft / Offline) + تذكرني + تبديل الحساب + Logout
- واجهة داكنة (أخضر #44BD32) + شريط جانبي يتقلّص تلقائياً + عربي/English (RTL تلقائي)
- إدارة النسخ (كل نسخة في `.minecraft/versions/<الاسم>`) مع بحث وفلترة وفتح المجلد
- المودات: Modrinth + CurseForge + Drag&Drop + تفعيل/تعطيل + شعار/وصف + تحذير عدم التوافق
- Resource Packs (تفعيل/تعطيل عبر options.txt) + Skins (Grid + عارض 3D + رفع إلى Mojang)
- الإعدادات (RAM 1–32GB، Java، JVM، الدقة، CurseForge Key) + SQLite + سجل تشغيل + Kill

## ملاحظات مهمة
- **Forge / NeoForge**: إدارة المودات تدعمهما، لكن **تثبيت اللودر تلقائياً عند التشغيل** مدعوم حالياً لـ **Vanilla وFabric وQuilt فقط**.
- **CurseForge**: احصل على API Key مجاني من console.curseforge.com وضعه في الإعدادات.
- بيانات التطبيق في `%AppData%\.gmu-launcher` (قاعدة SQLite، الـ skins، و`.minecraft`).
- فحص توافق المودات تقريبي (heuristic) ويعتمد على نطاق الإصدار المكتوب داخل المود.
- Minecraft علامة تجارية لـ Mojang AB / Microsoft، والمشروع غير تابع لهما.
