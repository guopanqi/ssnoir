# Noir Engraving capture report

| mode | shot | mean | p50 | p90 | p95 | p99 | black<2% | bright>20% |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| shape | 01-theater-street | 0.054 | 0.015 | 0.228 | 0.231 | 0.241 | 67.4% | 10.9% |
| shape | 02-warehouse-fog | 0.059 | 0.015 | 0.171 | 0.208 | 0.255 | 62.1% | 5.7% |
| shape | 03-alley-mouth | 0.021 | 0.000 | 0.109 | 0.143 | 0.218 | 85.6% | 1.6% |
| shape | 04-city-compression | 0.058 | 0.004 | 0.215 | 0.249 | 0.306 | 73.7% | 13.3% |
| line | 01-theater-street | 0.056 | 0.015 | 0.228 | 0.231 | 0.350 | 66.8% | 11.4% |
| line | 02-warehouse-fog | 0.061 | 0.015 | 0.175 | 0.214 | 0.261 | 61.4% | 6.3% |
| line | 03-alley-mouth | 0.022 | 0.000 | 0.109 | 0.143 | 0.218 | 85.4% | 1.6% |
| line | 04-city-compression | 0.061 | 0.006 | 0.220 | 0.255 | 0.316 | 72.5% | 14.0% |
| preprint | 01-theater-street | 0.049 | 0.015 | 0.200 | 0.207 | 0.328 | 66.0% | 10.0% |
| preprint | 02-warehouse-fog | 0.055 | 0.015 | 0.145 | 0.173 | 0.232 | 60.8% | 2.3% |
| preprint | 03-alley-mouth | 0.021 | 0.000 | 0.106 | 0.143 | 0.213 | 85.4% | 1.4% |
| preprint | 04-city-compression | 0.050 | 0.008 | 0.170 | 0.189 | 0.260 | 71.4% | 3.5% |
| final | 01-theater-street | 0.054 | 0.007 | 0.293 | 0.308 | 0.370 | 66.4% | 11.6% |
| final | 02-warehouse-fog | 0.069 | 0.007 | 0.153 | 0.285 | 0.326 | 61.2% | 7.1% |
| final | 03-alley-mouth | 0.032 | 0.007 | 0.146 | 0.153 | 0.313 | 85.6% | 3.5% |
| final | 04-city-compression | 0.066 | 0.007 | 0.285 | 0.308 | 0.456 | 71.6% | 14.5% |

## Capture timing

Total capture: 18.0s
- final: 5.1s (1.1 / 1.2 / 1.3 / 1.4s)
- shape: 3.1s (0.9 / 0.7 / 0.6 / 0.9s)
- line: 3.3s (1.0 / 0.9 / 0.7 / 0.7s)
- preprint: 3.7s (0.5 / 1.4 / 0.8 / 1.0s)

## Layer impact

| shot | shape→line mean | line→preprint mean | preprint→final mean | preprint→final black<2% |
|---|---:|---:|---:|---:|
| 01-theater-street | +0.002 | -0.007 | +0.005 | 0.4pp |
| 02-warehouse-fog | +0.002 | -0.006 | +0.014 | 0.4pp |
| 03-alley-mouth | +0.000 | -0.000 | +0.011 | 0.2pp |
| 04-city-compression | +0.003 | -0.011 | +0.017 | 0.2pp |
