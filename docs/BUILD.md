# Build (requires a computer with Unity)
1. Install Unity Hub and Unity 6 LTS (match ProjectVersion.txt) with Android Build Support (SDK/NDK/OpenJDK).
2. Open the project; let packages resolve. Run EditMode tests (Window > General > Test Runner).
3. File > Build Profiles > Android. APK: uncheck "Build App Bundle". AAB: check it. Use your own keystore (never commit it).
4. Firebase: create a project, add the Android app, download google-services.json locally (git-ignored), enable Email/Password, publish firebase/firestore.rules. Not integrated in code yet.
5. Start.io: not integrated yet; verify the official SDK docs for Unity 6 compatibility first.
## Release
git tag v0.1.0 && git push --tags, then create a Release on GitHub.
