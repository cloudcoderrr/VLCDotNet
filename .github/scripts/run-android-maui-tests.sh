#!/usr/bin/env bash
set -euo pipefail

adb wait-for-device
while [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" != "1" ]; do
  sleep 2
done

adb logcat -c || true

apk=$(find Tests/VLCDotNet.Tests.Maui/bin/Debug/net10.0-android -name '*-Signed.apk' 2>/dev/null | head -1)
if [ -z "${apk}" ]; then
  apk=$(find Tests/VLCDotNet.Tests.Maui/bin -name '*.apk' 2>/dev/null | head -1)
fi

echo "APK: '${apk}'"
ls -la Tests/VLCDotNet.Tests.Maui/bin/Debug/net10.0-android/ 2>/dev/null || true
test -n "${apk}"

echo "Installing ${apk}"
adb install -r "${apk}"
adb shell monkey -p com.vlcdotnet.tests -c android.intent.category.LAUNCHER 1

done_flag=""
done_contents=""
for _ in $(seq 1 120); do
  if done_contents=$(adb shell run-as com.vlcdotnet.tests cat files/test-output/DONE.txt 2>/dev/null); then
    printf '%s\n' "${done_contents}"
    done_flag=1
    break
  fi
  sleep 5
done

mkdir -p mobile-output/android
adb exec-out run-as com.vlcdotnet.tests tar c -C files test-output 2>/dev/null | tar x -C mobile-output/android || true

echo "---- vlc-log tail ----"
tail -n 60 mobile-output/android/test-output/vlc-log.txt || true

echo "---- logcat (vlc/mono/crash) ----"
adb logcat -d 2>/dev/null | grep -iE 'vlcdotnet|libvlc|mono-rt|AndroidRuntime|FATAL|DOTNET' | tail -n 80 || true

test -n "${done_flag}"

failures=$(printf '%s\n' "${done_contents}" | head -n 1 | tr -d '\r' || true)
case "${failures}" in
  ''|*[!0-9]*)
    echo "Unexpected DONE.txt contents; expected numeric failure count on line 1." >&2
    exit 1
    ;;
esac

if [ "${failures}" -ne 0 ]; then
  echo "Android test suite reported ${failures} failure(s)." >&2
  exit 1
fi
