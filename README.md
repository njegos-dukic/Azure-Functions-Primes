# Azure Functions (FaaS) - Distributed Prime Number Finder

<img src = "..\assets\Azure Functions.png" width = 150/>
---

- Functions are ideal when you're concerned only about the code running your service and not the underlying platform or infrastructure. They're commonly used when you need to perform work in response to an event (often via a REST request), timer, or message from another Azure service, and when that work can be completed quickly, within seconds or less.
- Serverless computing: The ability to run custom code on demand and at scale in the cloud.
- Azure will run code dynamically based on events.
---
- Distributed Azure Functions (Microservice) that accepts ranges in JSON format and outputs count (and values) of prime numbers in specified ranges.
- JSON Get request should be in the following format:
---
``` JSON
"scaleFactor": 10,
	
"ranges": 
[
	{
		"start": 3,
		"end": 7920
	},
	{
		"start": 12500,
		"end": 18999
	},
	{
		"start": 152,
		"end": 7921
	}
]
```
---
- Depending on the version selected, output is in presented in one of the following forms:
``` JSON
// Distributed Primes Separated Ranges

"output":
[
    {
        "total": 2629
    },
    {
        "range":
        {
            "start": "3",
            "end": "7920",
            "count": 999,
            "values": "3  5  7  11  13  ...  7879  7883  7901  7907  7919"
        }
    },
    {
        "range":
        {
            "start": "12500",
            "end": "18999",
            "count": 666,
            "values": "12503  12511  ...  18973  18979"
        }
    },
    {
        "range":
        {
            "start": "152",
            "end": "7921",
            "count": 964,
            "values": "157  163  167  173  ...  7907  7919"
        }
    }
]
```
---
``` JSON
// Distributed Primes Combined Ranges

{
    "output": "Count: 2629"
}
```
