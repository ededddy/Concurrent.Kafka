# An implementation of Concurrent Kafka Consumer

## Motivations
1. Fun
2. Scaling Kafka consumers is hard.  
		Cannot always just add more topic partitions,  
		& Adding more machines to a clusters.  
		Internal concurrency is a neat trick to gain performance.
3. Coding style varies from dev to dev.  
		Causing performance loss.  
		Need to at least provide a minimal scoping for devs to write
		performant and readable code.

## Goals of this repo

1. Functional & easy to use NuGet Package for the community to use.
2. Implement multiple concurrent strategies for developers to use.
3. Provide benchmarks compared with 0 concurrency


## TBC
