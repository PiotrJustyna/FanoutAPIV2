docker build -t fanout-api-v1 -f ./dockerfile ./ &&
  docker run -it -e "FANOUT-HELPER-API-HOST"="host.docker.internal:2345" -p 5432:80 --rm fanout-api-v1