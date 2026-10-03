# Noir Engraving capture report

| mode | shot | mean | p50 | p90 | p95 | p99 | black<2% | bright>20% |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| shape | 01-theater-street | 0.054 | 0.015 | 0.228 | 0.231 | 0.241 | 67.4% | 10.9% |
| shape | 02-warehouse-fog | 0.059 | 0.015 | 0.171 | 0.208 | 0.255 | 62.1% | 5.7% |
| shape | 03-alley-mouth | 0.021 | 0.000 | 0.109 | 0.143 | 0.218 | 85.6% | 1.6% |
| shape | 04-city-compression | 0.045 | 0.000 | 0.221 | 0.258 | 0.311 | 81.9% | 13.4% |
| line | 01-theater-street | 0.055 | 0.015 | 0.228 | 0.231 | 0.337 | 66.8% | 11.2% |
| line | 02-warehouse-fog | 0.061 | 0.015 | 0.175 | 0.214 | 0.261 | 61.4% | 6.3% |
| line | 03-alley-mouth | 0.022 | 0.000 | 0.109 | 0.143 | 0.218 | 85.4% | 1.6% |
| line | 04-city-compression | 0.047 | 0.001 | 0.223 | 0.260 | 0.316 | 81.2% | 13.6% |
| preprint | 01-theater-street | 0.048 | 0.015 | 0.199 | 0.207 | 0.313 | 66.0% | 9.9% |
| preprint | 02-warehouse-fog | 0.055 | 0.015 | 0.145 | 0.173 | 0.232 | 60.8% | 2.3% |
| preprint | 03-alley-mouth | 0.021 | 0.000 | 0.106 | 0.143 | 0.213 | 85.4% | 1.4% |
| preprint | 04-city-compression | 0.038 | 0.008 | 0.171 | 0.195 | 0.256 | 79.8% | 4.3% |
| final | 01-theater-street | 0.053 | 0.007 | 0.292 | 0.308 | 0.354 | 66.4% | 11.4% |
| final | 02-warehouse-fog | 0.069 | 0.007 | 0.153 | 0.285 | 0.326 | 61.2% | 7.1% |
| final | 03-alley-mouth | 0.032 | 0.007 | 0.146 | 0.153 | 0.313 | 85.6% | 3.5% |
| final | 04-city-compression | 0.052 | 0.007 | 0.258 | 0.297 | 0.548 | 80.3% | 12.2% |

## Capture timing

Total capture: 14.2s
- final: 4.8s (1.1 / 1.2 / 1.3 / 1.2s)
- shape: 1.9s (0.4 / 0.5 / 0.5 / 0.5s)
- line: 2.4s (0.5 / 0.5 / 0.8 / 0.7s)
- preprint: 2.1s (0.6 / 0.5 / 0.5 / 0.5s)

## Layer impact

| shot | shape→line mean | line→preprint mean | preprint→final mean | preprint→final black<2% |
|---|---:|---:|---:|---:|
| 01-theater-street | +0.001 | -0.007 | +0.005 | 0.5pp |
| 02-warehouse-fog | +0.002 | -0.006 | +0.014 | 0.4pp |
| 03-alley-mouth | +0.000 | -0.000 | +0.011 | 0.2pp |
| 04-city-compression | +0.001 | -0.009 | +0.014 | 0.4pp |
