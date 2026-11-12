MediaDesk 0.3.0 — Windows full installer / portable release builder

هذه الحزمة هي أداة إنتاج التثبيت على جهاز ويندوز الذي بُني عليه MediaDesk.
ليست ملف MediaDesk Setup.exe جاهزًا؛ يلزم توليده من EXE الذي تم اختباره على جهازك.

المخرجات المتوقعة عند نجاح العملية:
D:\MediaDesk-Windows\dist\out\MediaDesk-Setup-v0.3.0-x64.exe
D:\MediaDesk-Windows\dist\out\MediaDesk-Portable-v0.3.0-x64.zip

المتطلبات على جهاز البناء فقط:
- نسخة MediaDesk R3 المبنية سابقًا (EXE التجريبي الذي اختبرته)
- Python 3.11 مع yt-dlp وyt-dlp-ejs المثبتين مسبقًا
- اتصال PyPI عند الحاجة إلى PyInstaller (مرة واحدة)
- حزمة FFmpeg الكاملة مع ffmpeg.exe و ffprobe.exe و ffplay.exe
- Node.js إصدار 22 أو أحدث على جهاز البناء ليُضمَّن في الحزمة
- Inno Setup 6 أو 7 (ISCC.exe)
- ملفات إضافات المتصفحات v030-r3

بعد فك ZIP، نفذ الأمر التالي من مجلد السكربت:
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-MediaDesk-Release.ps1

إن لم يكن Inno Setup متوفرًا، ثبتّه على جهاز البناء ثم أعد المحاولة:
winget install --id JRSoftware.InnoSetup --exact --source winget

البرنامج النهائي مستقل عن محاكي أندرويد وعن تثبيت .NET وPython على الأجهزة الأخرى.
البرنامج يحمل الأدوات في مجلد tools داخل مسار التثبيت، ولا يعني ذلك
أن MediaDesk.exe بمفرده يحتوي جميع المكونات؛ انقل ملف Setup EXE أو ZIP المحمول.

تكامل Chrome/Edge: ملفات الإضافة مضمّنة، وتحتاج إلى تحميلها عبر Extensions > Developer mode > Load unpacked.
Firefox: التثبيت المؤقت عبر about:debugging، والتثبيت الدائم يتطلب إضافة موقّعة من Mozilla.
يسجّل المثبت بروتوكول mediadesk:// للمستخدم الحالي.

مهم: تضمين أدوات مفتوحة المصدر في مثبت قابل للنشر يلزمك بتراخيصها، ومنها
GPLv3+ الخاصة بحزم yt-dlp المجمدة بواسطة PyInstaller، وترخيص بناء FFmpeg المستخدم.
قد توجد شروط إضافية عند النشر العام/التجاري. راجع THIRD_PARTY_NOTICES.txt.

السكريبت لا يستبدل النسخة المستقرة، ويستخدم مجلد عمل منفصل.
لا يمكن ضمان عمل جميع خدمات تنزيل الفيديو بسبب قيود المواقع وDRM وحقوق المحتوى.
