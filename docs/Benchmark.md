# Benchmark

This benchmark is measured with a proprietary project in my company.

#### Project size

| Project                   | Count | Size      |
| :------------------------ | ----: | --------: |
| Assembly-CSharp-firstpass |    10 |   158,858 |
| Assembly-CSharp           | 1,377 | 7,509,543 |
| Assembly-CSharp-Editor    |   223 | 1,967,076 |
| Total                     | 1,610 | 9,635,477 |

### Benchmark Result

#### Unity Mono 3

| Project                   | Duration (sec) |
| :------------------------ | -------------: |
| Assembly-CSharp-firstpass |           1.72 |
| Assembly-CSharp           |          12.78 |
| Assembly-CSharp-Editor    |           4.34 |
| Total                     |          18.84 |

#### Roslyn (Full)

| Project                   | Duration (sec) |
| :------------------------ | -------------: |
| Assembly-CSharp-firstpass |    3.96 + 0.39 |
| Assembly-CSharp           |    6.32 + 0.83 |
| Assembly-CSharp-Editor    |    2.16 + 0.46 |
| Total                     |    14.12 (75%) |

#### Roslyn (Incremental)

| Project                   | Duration (sec) |
| :------------------------ | -------------: |
| Assembly-CSharp-firstpass |    1.16 + 0.48 |
| Assembly-CSharp           |    4.60 + 0.82 |
| Assembly-CSharp-Editor    |    1.67 + 0.47 |
| Total                     |      9.2 (49%) |

#### Roslyn (Incremental with embedded MdbWriter)

| Project                   | Duration (sec) |
| :------------------------ | -------------: |
| Assembly-CSharp-firstpass |           1.31 |
| Assembly-CSharp           |           3.60 |
| Assembly-CSharp-Editor    |           1.00 |
| Total                     |     5.91 (31%) |
