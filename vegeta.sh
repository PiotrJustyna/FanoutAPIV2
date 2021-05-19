#!/bin/sh
clear
echo "GET http://host.docker.internal:5432/helloworld?slaMs=2000" | vegeta attack -redirects=0 -duration=10s -rate=300 | tee results.bin | vegeta report
cat results.bin | vegeta report -type="hist[0,500ms,600ms,700ms,800ms,900ms,1s,2s,3s,4s,5s,10s]"