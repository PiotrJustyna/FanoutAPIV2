# readme

Fanout API V1 - the project demonstrates how a task-heavy Web API would behave. The task fanout is done internally (in API runtime), the API is task-heavy.

## parameters

Exposed are three parameters:

* `slaMs` - SLA expressed in milliseconds. This value controls the time after which the API controller is supposed to order its `CancellationTokenSource` to time out and cancel that source's cancellation tokens.
* `tasksPerRequest` - controls how many asynchronous tasks should be created per request.
* `taskDelayMs` - each task performs arbitrary work of a simple `Task.Delay`. `taskDelayMs` controls how long that delay should be. The value is expressed in milliseconds.

## example

`http://localhost:5432/helloworld?slaMs=2000&tasksPerRequest=1000&taskDelayMs=500`

The request orders the API to:

* terminate the request if it's not completed under 2000ms
* start 1000 worker tasks per request and await them trying to best utilize the given SLA (`slaMs`)
* artificially (and asynchronously) delay each worker tasks by 500ms in order to mimic real (e.g. I/O) work

## usage

1. To run the API in docker, execute: `run-api-docker.sh`.
2. To run the load test, execute: `run-vegeta-docker.sh`.
3. To change the parameters of the load test, modify `vegeta.sh`
4. To access the API manually, use the following URL (while the API is running in docker, otherwise the port could be different): `http://localhost:5432/helloworld?slaMs=2000&tasksPerRequest=1000&taskDelayMs=500`