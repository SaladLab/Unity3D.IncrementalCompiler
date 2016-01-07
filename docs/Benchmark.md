# Benchmark

### How to measure

Benchmark is done with following steps.
 1. Run Unity3D.
 1. Open a target project.
 1. Make unity build scripts by editing one C# source for warming up.
 1. Do previous step again and measure compilation time.
    - Edit a script in Assembly-CSharp (not plugins nor editor)
    - Compilation time is recorded in UniversalCompiler log.

### Test environment

- CPU: Intel Core i5-4690 (3.50 GHz)
- RAM: 8 GB
- OS: Windows 10 Pro (64 bits)
- SSD: Samsung SSD 840 EVO 1TB
- Unity 5.3.1f1 (Win64)

### Target projects

 1. Generated project whose script size is 1.4 MB (sample/Benchmark)
 1. Generated project whose script size is 6.7 MB (sample/Benchmark)
 1. In-house real project whose script size is 9.4 MB

### Tested compiler

 1. Unity default compiler (Mono3)
 1. C# 6 roslyn compiler (CSC6)
 1. Incremental C# 6 Compiler with default configuration (ICS6)
 1. Incremental C# 6 Compiler with "WhenNoSourceChange" configuration (ICS6@)

### Results

#### Project 1 (Generated project: 1.4 MB)

This project is generated with `SourcePopulate.py 30 20 200` in ./samples/Benchmark.

##### Project size

| Project  | Count | Size      |
| :------- | ----: | --------: |
| Plugins  |    20 |   122,980 |
| Scripts  |   200 | 1,175,260 |
| Editor   |    30 |   181,409 |
| Total    |   250 | 1,479,649 |

##### Compilation time (sec)

|           | Mono3 | CSC6  | ICS6  | ICS6@ |
| :-------- | ----: | ----: | ----: | ----: |
| Scripts   |  2.23 |  3.52 |  0.95 |  0.95 |
| Editor    |  0.60 |  2.88 |  0.23 |  0.17 |
| Total     |  2.83 |  6.40 |  1.18 |  1.12 |
| Total (%) |  100% |  226% |   41% |   39% |

For small size project, it's hard to get significant benefit.
2.83 seconds with Mono3 is not bad for work.

#### Project 2 (Generated project: 6.7 MB)

This project is generated with `SourcePopulate.py 200 20 1000` in ./samples/Benchmark.

##### Project size

| Project  | Count | Size      |
| :------- | ----: | --------: |
| Plugins  |    20 |   122,980 |
| Scripts  | 1,000 | 5,642,460 |
| Editor   |   200 | 1,130,519 |
| Total    | 1,220 | 6,895,959 |

##### Compilation time (sec)

|           | Mono3 | CSC6  | ICS6  | ICS6@ |
| :-------- | ----: | ----: | ----: | ----: |
| Scripts   | 22.82 |  4.72 |  2.68 |  2.68 |
| Editor    |  1.88 |  3.08 |  0.59 |  0.17 |
| Total     | 24.70 |  7.80 |  3.27 |  2.85 |
| Total (%) |  100% |   31% |   13% |   11% |

After getting mono3 result, compilation time seems to be too long.
Test was repeated several times to make sure but same result.
There should be a corner case for mono compiler.

#### Project 3 (In-house project: 9.4 MB)

This project is my company project and not allowed to be open.
But this is a real project and worth sharing result.

##### Project size

| Project  | Count | Size      |
| :------- | ----: | --------: |
| Plugins  |    10 |   158,858 |
| Scripts  | 1,377 | 7,509,543 |
| Editor   |   223 | 1,967,076 |
| Total    | 1,610 | 9,635,477 |

##### Compilation time (sec)

|           | Mono3 | CSC6  | ICS6  | ICS6@ |
| :-------- | ----: | ----: | ----: | ----: |
| Scripts   |  8.30 |  5.67 |  3.50 |  3.52 |
| Editor    |  2.83 |  3.27 |  1.10 |  0.19 |
| Total     | 11.13 |  8.94 |  4.60 |  3.71 |
| Total (%) |  100% |   80% |   41% |   33% |

This result is first place that I consider to make this incremental compiler.
Edit & review workflow doesn't go well with Mono3 compilation time.
Everytime source is modified, I always look hard at loading indicator impatiently.
But incremental compiler with aggresive WhenNoSourceChange gives a way faster compilation
speed and make me happy, still I can feel time fly but bearable.
