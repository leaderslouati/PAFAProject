# PAFA — DAX Measures Reference

> Paste these measures into Power BI Desktop → Modeling → New Measure.
> They are designed for the **star schema** described in `VIEWS_ANALYSIS.md`.

---

## 1. Core Counts

```dax
// Total number of fact rows (shipper × product class × month combinations)
Count Total =
COUNTROWS('fact_read_performance')
```

```dax
// Total compliant rows (is_compliant = 1)
Count Compliant =
CALCULATE(
    COUNTROWS('fact_read_performance'),
    'fact_read_performance'[is_compliant] = 1
)
```

---

## 2. Compliance Percentage (per shipper, filtered by slicers)

```dax
// Compliance % — respects all active slicer filters
Compliance % =
DIVIDE(
    [Count Compliant],
    [Count Total],
    0
) * 100
```

---

## 3. Industry Average (ignores Shipper filter, keeps Product Class + Date)

```dax
// Industry Average % — removes the shipper filter so we get
// the average across ALL shippers, but still respects
// Product Class and Date filters from slicers.
Industry Average % =
CALCULATE(
    [Compliance %],
    REMOVEFILTERS('vw_dim_shipper')
)
```

---

## 4. Read Performance Percentage (average)

```dax
Read Performance Avg =
AVERAGE('fact_read_performance'[read_perf_pct])
```

```dax
// Industry-wide read performance (ignores shipper filter)
Read Performance Industry Avg =
CALCULATE(
    AVERAGE('fact_read_performance'[read_perf_pct]),
    REMOVEFILTERS('vw_dim_shipper')
)
```

---

## 5. Estimated Percentage (average)

```dax
Estimated Pct Avg =
AVERAGE('fact_read_performance'[estimated_pct])
```

```dax
Estimated Pct Industry Avg =
CALCULATE(
    AVERAGE('fact_read_performance'[estimated_pct]),
    REMOVEFILTERS('vw_dim_shipper')
)
```

---

## 6. Total Sites

```dax
Total Sites =
SUM('fact_read_performance'[total_sites])
```

---

## 7. Month-over-Month Change

```dax
// Requires dim_date marked as Date Table
MoM Change % =
VAR _currentValue = [Compliance %]
VAR _previousValue =
    CALCULATE(
        [Compliance %],
        DATEADD('vw_dim_date'[date_key], -1, MONTH)
    )
RETURN
    IF(
        NOT ISBLANK(_previousValue),
        _currentValue - _previousValue,
        BLANK()
    )
```

---

## 8. Shipper Display Name (conditional anonymisation)

> **Use this only if you have a single dataset with both alias and real names.**
> Preferred approach: use separate datasets (v_parr_industry / v_parr_pac).

```dax
Shipper Display =
IF(
    SELECTEDVALUE('vw_dim_shipper'[shipper_code]) = USERPRINCIPALNAME(),
    SELECTEDVALUE('vw_dim_shipper'[real_shipper_name]),
    SELECTEDVALUE('vw_dim_shipper'[alias_code])
)
```

---

## 9. Rank Measures (for leaderboard views)

```dax
Rank Best =
RANKX(
    ALLSELECTED('vw_dim_shipper'[shipper_code]),
    [Estimated Pct Avg],
    , ASC, Dense
)
```

```dax
Rank Worst =
RANKX(
    ALLSELECTED('vw_dim_shipper'[shipper_code]),
    [Estimated Pct Avg],
    , DESC, Dense
)
```

---

## 10. Distribution Bin (for histogram visuals)

```dax
Pct Bin =
SWITCH(
    TRUE(),
    [Estimated Pct Avg] < 10,  "00-10%",
    [Estimated Pct Avg] < 20,  "10-20%",
    [Estimated Pct Avg] < 30,  "20-30%",
    [Estimated Pct Avg] < 40,  "30-40%",
    [Estimated Pct Avg] < 50,  "40-50%",
    [Estimated Pct Avg] < 60,  "50-60%",
    [Estimated Pct Avg] < 70,  "60-70%",
    [Estimated Pct Avg] < 80,  "70-80%",
    [Estimated Pct Avg] < 90,  "80-90%",
    "90-100%"
)
```

---

## 11. UNC Threshold Reference Line

```dax
// Returns the MinReadPercentage threshold for the selected product class
UNC Threshold =
SELECTEDVALUE('fact_read_performance'[unc_threshold], 97.5)
```

---

## RLS — Manage Roles (Power BI Desktop)

### Role: `Shipper` (for Industry / Schedule 2A)

In **Modeling → Manage roles → New role → Shipper**,
add this DAX filter on table `vw_dim_shipper`:

```dax
[shipper_code] = USERPRINCIPALNAME()
```

### Role: `PAC` (for Schedule 2B)

No filter — PAC/PAFA admins see all data.

---

## Power BI Model Relationships

| From (Dimension)                    | To (Fact)                                     | Cardinality | Cross-filter |
|-------------------------------------|-----------------------------------------------|-------------|--------------|
| `vw_dim_date[date_key]`            | `fact_read_performance[report_month]`          | 1:*         | Single →     |
| `vw_dim_shipper[shipper_code]`     | `fact_read_performance[shipper_code]`          | 1:*         | Single →     |
| `product_classes[Code]`            | `fact_read_performance[product_class]`         | 1:*         | Single →     |

> **Mark `vw_dim_date` as Date Table** (column: `date_key`) for time intelligence.
