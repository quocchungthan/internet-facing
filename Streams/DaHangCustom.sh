## to run the docker containers
mkdir -p DaHangSelfhost
cp -r DaHang/.docker/selfhost/ DaHangSelfhost/ && cd DaHangSelfhost && docker compose -f compose.yml up -d