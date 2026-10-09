# Noir Engraving capture report

| mode | shot | mean | p50 | p90 | p95 | p99 | black<2% | bright>20% |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| shape | 01-theater-street | 0.054 | 0.015 | 0.228 | 0.231 | 0.241 | 67.4% | 10.9% |
| shape | 02-warehouse-fog | 0.059 | 0.015 | 0.171 | 0.208 | 0.255 | 62.1% | 5.7% |
| shape | 03-alley-mouth | 0.025 | 0.000 | 0.140 | 0.152 | 0.180 | 84.3% | 0.6% |
| shape | 04-city-compression | 0.045 | 0.000 | 0.221 | 0.258 | 0.311 | 81.9% | 13.4% |
| line | 01-theater-street | 0.055 | 0.015 | 0.228 | 0.231 | 0.337 | 66.8% | 11.2% |
| line | 02-warehouse-fog | 0.061 | 0.015 | 0.175 | 0.214 | 0.261 | 61.4% | 6.3% |
| line | 03-alley-mouth | 0.025 | 0.000 | 0.140 | 0.152 | 0.180 | 84.0% | 0.5% |
| line | 04-city-compression | 0.047 | 0.001 | 0.223 | 0.260 | 0.316 | 81.2% | 13.6% |
| preprint | 01-theater-street | 0.048 | 0.015 | 0.199 | 0.207 | 0.313 | 66.3% | 9.9% |
| preprint | 02-warehouse-fog | 0.055 | 0.015 | 0.145 | 0.172 | 0.231 | 61.0% | 2.2% |
| preprint | 03-alley-mouth | 0.024 | 0.000 | 0.139 | 0.149 | 0.174 | 84.0% | 0.5% |
| preprint | 04-city-compression | 0.037 | 0.008 | 0.171 | 0.195 | 0.255 | 80.4% | 4.3% |
| final | 01-theater-street | 0.052 | 0.007 | 0.292 | 0.308 | 0.354 | 66.9% | 11.2% |
| final | 02-warehouse-fog | 0.068 | 0.007 | 0.153 | 0.284 | 0.320 | 61.5% | 7.0% |
| final | 03-alley-mouth | 0.034 | 0.007 | 0.151 | 0.153 | 0.304 | 84.2% | 2.7% |
| final | 04-city-compression | 0.051 | 0.007 | 0.258 | 0.297 | 0.546 | 81.1% | 12.1% |

## Capture timing

Total capture: 4.6s
- final: 1.5s (0.5 / 0.3 / 0.3 / 0.3s)
- shape: 0.6s (0.2 / 0.1 / 0.1 / 0.1s)
- line: 0.8s (0.2 / 0.2 / 0.2 / 0.2s)
- preprint: 1.2s (0.3 / 0.3 / 0.3 / 0.3s)

## Layer impact

| shot | shape→line mean | line→preprint mean | preprint→final mean | preprint→final black<2% |
|---|---:|---:|---:|---:|
| 01-theater-street | +0.001 | -0.008 | +0.005 | 0.6pp |
| 02-warehouse-fog | +0.002 | -0.006 | +0.013 | 0.5pp |
| 03-alley-mouth | +0.000 | -0.000 | +0.009 | 0.2pp |
| 04-city-compression | +0.001 | -0.009 | +0.014 | 0.7pp |
