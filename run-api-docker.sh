docker build -t fanout-api-v1 -f ./dockerfile ./ &&
  docker run -it -p 5432:80 --rm fanout-api-v1