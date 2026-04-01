using PAFA.Domain.Interfaces;

namespace PAFA.Extraction.Mapping;

public static class MetricValueMapper
{
    // ── Colonnes numériques mappées avec leur MetricKey normalisée ──
    // Clé : alias(es) possible dans l'en-tête Excel (normalisé)
    // Valeur : MetricKey stocké en base (snake_case, stable)
    private static readonly Dictionary<string[], string> MetricColumns = new()
    {
        // Lecture & performance
        { new[]{"readperformancepct","readperformance","readperf"},        "read_performance_pct"   },
        { new[]{"estimatedreadpct","estimatedreads","est"},                "estimated_read_pct"     },
        { new[]{"transferreadsucc","transferread"},                         "transfer_read_succ_pct" },
        { new[]{"class4aqreadpct"},                                        "class4_aq_read_pct"     },
        { new[]{"class23mprpct"},                                          "class23_mpr_pct"        },

        // Compteurs de sites
        { new[]{"totalsitecount","totalsites"},                            "total_site_count"       },
        { new[]{"checkreadcount","checkreads"},                            "check_read_count"       },
        { new[]{"nometerspr","nometer"},                                   "no_meter_spr_count"     },
        { new[]{"dataflowsreceived","dataflows"},                          "data_flows_received"    },
        { new[]{"transferreadtotal"},                                      "transfer_read_total"    },
        { new[]{"invalidreadcount"},                                       "invalid_read_count"     },

        // No-reads (cumulatifs)
        { new[]{"noreadcount1yr","noreads1yr"},                            "no_read_count_1yr"      },
        { new[]{"noreadcount2yr","noreads2yr"},                            "no_read_count_2yr"      },
        { new[]{"noreadcount3yr","noreads3yr"},                            "no_read_count_3yr"      },
        { new[]{"noreadcount4yr","noreads4yr"},                            "no_read_count_4yr"      },

        // AQ & corrections
        { new[]{"aqcorrectioncount"},                                      "aq_correction_count"    },
        { new[]{"stdcorrfactorcount"},                                     "std_corr_factor_count"  },
        { new[]{"replacedreadcount"},                                      "replaced_read_count"    },
        { new[]{"class1threshsites"},                                      "class1_thresh_sites"    },
        { new[]{"aqoverduecount","aqoverdue"},                             "aq_overdue_count"       },

        // Energy theft
        { new[]{"energytheftcount"},                                       "energy_theft_count"     },
        { new[]{"theftobjectioncount"},                                    "theft_objection_count"  },

        // Reclassification & divers
        { new[]{"pc2to4convcount"},                                        "pc2_to4_conv_count"     },
        { new[]{"igtknownissuecount"},                                     "igt_known_issue_count"  },
        { new[]{"comrrejections"},                                         "comr_rejections"        },
        { new[]{"class4vacantsites"},                                      "class4_vacant_sites"    },
    };

    /// <summary>
    /// Mappe un RawDataRow → liste de MetricValue (une par colonne numérique trouvée).
    /// Les colonnes absentes ou non-numériques sont ignorées silencieusement.
    /// </summary>
    public static IEnumerable<MetricValue> MapToMetricValues(
        RawDataRow row,
        Guid ingestionFileId,
        DateOnly reportingPeriod,
        string uploadedBy = "SYSTEM")
    {
        var ssc = GetCell(row, "shippershortcode", "ssc", "code") ?? string.Empty;

        foreach (var (aliases, metricKey) in MetricColumns)
        {
            var raw = GetCell(row, aliases);
            if (raw is null) continue;

            var value = ParseDecimal(raw);
            if (value is null) continue;

            yield return new MetricValue
            {
                Id = Guid.NewGuid(),
                IngestionFileId = ingestionFileId,
                ShipperShortCode = ssc,
                MetricKey = metricKey,
                Value = value.Value,
                ReportingPeriod = reportingPeriod,
                CreatedBy = uploadedBy
            };
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static string? GetCell(RawDataRow row, params string[] aliases)
    {
        foreach (var a in aliases)
            if (row.Cells.TryGetValue(a, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return null;
    }

    private static decimal? ParseDecimal(string raw)
    {
        raw = raw.Replace("%", "").Trim();
        if (!decimal.TryParse(raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var d)) return null;

        // Fraction Excel (0.975) → % (97.5)
        if (d is > 0 and <= 1.0m && raw.Contains('.') && d != 1m)
            d *= 100m;

        return Math.Round(d, 4);
    }
}