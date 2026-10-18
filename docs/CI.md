# التحقق والبناء السحابي (من الهاتف)

## 1) اختبارات النواة — بدون أي ترخيص (جاهزة الآن)
GitHub ▸ المستودع ▸ **Actions** ▸ **core-tests** ▸ **Run workflow**.
- تترجم النواة بـ .NET (بدون Unity) وتشغّل اختبارات NUnit الخاصة بها (35 اختبارًا من أصل 39؛ الأربعة الباقية تعتمد على JsonUtility وتعمل داخل Unity فقط).
- إن فشلت الترجمة أو أي اختبار، افتح الخطوة الحمراء وانسخ الرسالة وأرسلها لي لإصلاحها.
- تعمل أيضًا تلقائيًا عند كل push.

## 2) اختبارات Unity وبناء APK/AAB — تحتاج ترخيص Unity (اختياري)
> تحقق من الخطوات الحالية في https://game.ci/docs/github/activation لأن Unity قد غيّرت نظام التراخيص. لم أتمكن من التحقق منها (لا إنترنت عندي).
1. Actions ▸ **unity-activation** ▸ Run workflow. حمّل الملف `.alf` من الـ Artifacts.
2. افتح https://license.unity3d.com/manual (بحساب Unity) وارفع الـ `.alf` واختر النسخة الشخصية (Personal) ونزّل ملف `.ulf`.
3. Settings ▸ Secrets and variables ▸ Actions ▸ New repository secret:
   - `UNITY_LICENSE` = محتوى ملف `.ulf` كاملًا
   - `UNITY_EMAIL` و `UNITY_PASSWORD` = بيانات حساب Unity. أدخلها في GitHub فقط، ولا ترسلها لي.
4. Actions ▸ **unity** ▸ Run workflow ▸ اختر apk أو aab. الناتج في Artifacts.
- يجب أن يطابق `UNITY_VERSION` في ملفات الـ workflow ما في `ProjectSettings/ProjectVersion.txt`، وأن تكون صورة Docker لهذا الإصدار موجودة: https://game.ci/docs/docker/versions
- الـ APK الناتج موقّع توقيع تجريبيًا (debug) للاختبار فقط. النشر على Google Play يحتاج keystore خاصًا بك (لم يُنفَّذ، ولا تضع المفاتيح في المستودع).
