docker build -t vegeta -f ./vegeta-dockerfile ./ &&
  docker run -it -p 6543:80 --rm vegeta