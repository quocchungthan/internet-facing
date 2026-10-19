#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")/.."
mkdir -p public/media
tls_options=()
if curl --version | grep -qi schannel; then
  tls_options+=(--ssl-revoke-best-effort)
fi
fetch() {
  local filename="$1" url="$2"
  curl "${tls_options[@]}" --fail --location --retry 3 --retry-delay 10 --connect-timeout 15 --max-time 90 --user-agent '30s-EducationalWorkshop/0.1 (attributed Wikimedia Commons reference photographs)' \
    --output "public/media/$filename" "$url"
}
fetch raize-engine.jpg 'https://upload.wikimedia.org/wikipedia/commons/8/80/2021_Toyota_Raize_1.0_Turbo_S_GR_Sport_A250RA_1KR-VET_engine_%2820210521%29.jpg'
fetch raize-exterior.jpg 'https://upload.wikimedia.org/wikipedia/commons/4/49/2021_Toyota_Raize_1.0T_GR_Sport_%28Indonesia%29_front_view.jpg'
fetch impala-exterior.jpg 'https://upload.wikimedia.org/wikipedia/commons/e/ee/1967_Chevrolet_Impala_4_door_Hardtop.jpg'
fetch smallblock-reference.jpg 'https://thumb.wikimedia.org/wikipedia/commons/thumb/9/95/Chevrolet_327_Cubic_Inch_Small-Block_V8_Engine_Marinized_By_Chris-Craft%2C_Incorporated_Of_Algonac%2C_Michigan%3B_Mounted_On_An_Engine_Hoist.jpg/1280px-Chevrolet_327_Cubic_Inch_Small-Block_V8_Engine_Marinized_By_Chris-Craft%2C_Incorporated_Of_Algonac%2C_Michigan%3B_Mounted_On_An_Engine_Hoist.jpg'
