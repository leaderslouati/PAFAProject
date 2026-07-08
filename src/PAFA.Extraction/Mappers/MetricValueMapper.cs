using PAFA.Domain.Entities;
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
        { new[]{"no_meter_pct","nometerflowpct"},                  "no_meter_pct"                  },
        { new[]{"transfer_read_total","transfertotal"},             "transfer_read_total"            },
        { new[]{"mre01026pct"},                                    "mre01026_pct"                  },
        { new[]{"mre01027pct"},                                    "mre01027_pct"                  },
        { new[]{"mre01028pct"},                                    "mre01028_pct"                  },
        { new[]{"mre01029pct"},                                    "mre01029_pct"                  },
        { new[]{"mre01030pct"},                                    "mre01030_pct"                  },
        { new[]{"aqcorrectionreason01"},                           "aq_correction_reason_01"        },
        // ... (01 à 09)
        { new[]{"class1abovethreshcount"},                         "class1_above_thresh_count"      },
        { new[]{"class1abovethreshaqgwh"},                         "class1_above_thresh_aq_gwh"     },
        { new[]{"class1reclassifiedcount"},                        "class1_reclassified_count"      },
        { new[]{"aqreadperf293kpct"},                              "aq_read_perf_monthly_293k_pct"  },
        { new[]{"aqreadperfsmtpct"},                               "aq_read_perf_smart_amr_pct"     },
        { new[]{"aqreadperfannualpct"},                            "aq_read_perf_annual_pct"        },
        { new[]{"aqatriskgwh"},                                    "aq_at_risk_gwh"                 },
        { new[]{"aqatriskpct"},                                    "aq_at_risk_pct"                 },
        { new[]{"theftclaimpct"},                                  "theft_claim_obj_pct"            },
        { new[]{"theftenergypct"},                                 "theft_claim_energy_pct"         },
        { new[]{"theftwdpct"},                                     "theft_wd_obj_pct"               },
        { new[]{"class3convcountpc"},                              "class3_conv_count"              },
        { new[]{"class3convaqgwh"},                                "class3_conv_aq_gwh"             },
        { new[]{"class3convpct"},                                  "class3_conv_pct"                },
        { new[]{"minpctreqpct"},                                   "min_pct_req_pct"                },
        { new[]{"minpctnotmetcount"},                              "min_pct_not_met_count"          },
        { new[]{"mprnremovedpct"},                                 "mprn_removed_pct"               },
        { new[]{"mustreadagepct"},                                 "must_read_age_pct"              },
        { new[]{"mprnenteringcount"},                              "mprn_entering_count"            },
        { new[]{"comrcount"},                                      "comr_count"                     },
        { new[]{"comrrejectrecvpct"},                              "comr_reject_recv_pct"           },
        { new[]{"comrrejectraisedpct"},                            "comr_reject_raised_pct"         },
        { new[]{"vacantinmonthcount"},                             "vacant_in_month_count"          },
        { new[]{"vacanteodcount"},                                 "vacant_eod_count"               },
        { new[]{"vacantproportionpct"},                            "vacant_proportion_age_pct"      },
    };

    /// <summary>
    /// Mappe un RawDataRow → liste de MetricValue (une par colonne numérique trouvée).
    /// Les colonnes absentes ou non-numériques sont ignorées silencieusement.
    ///
    /// Comment se fait le remplissage en base :
    ///   PersistFilesHandler (Step 3) télécharge le fichier Blob, appelle ExcelInspectionService,
    ///   construit les RawDataRow puis appelle cette méthode pour chaque ligne.
    ///   Le résultat est inséré dans la table metric_values via IUnitOfWork.MetricValues.AddRangeAsync().
    ///   Les vues SQL (vw_2a*, vw_2b*) lisent ensuite metric_values filtrées par report_code.
    ///
    /// Flux complet :
    ///   SharePoint XLSX → Blob /inbound/ → ExcelInspectionService → RawDataRow
    ///   → MapToMetricValues (ici) → metric_values (DB) → vues SQL → Power BI Report Builder
    ///
    /// Mapping fichier → report_code (source officielle : Files/PARR Reports - Mapping.xlsx) :
    ///   MOD520A__PAF_Reports_*.xlsx → 2A.1-2A.10 et 2B.1-2B.10  (déterminé par onglet)
    ///   EUC09_Reporting_PAC_*.xlsx  → 2A.11a, 2A.11b, 2B.14a, 2B.14b
    ///   Rpt_1364_PARR AQ*.xlsx      → 2B.11a à 2B.11h
    ///   AQ at Risk*.xlsx            → 2A.13, 2B.16
    ///   Read Performance by Shipper → 2A.12a/b/c, 2B.15a/b/c
    ///   Confirmed Energy Theft*.xlsx → 2A.14, 2B.17
    ///   Supply Points Reclassified  → 2A.15, 2B.18
    ///   Supply Points Min Threshold → 2A.16, 2B.19
    ///   Report 1A/1B (Vacant)       → 2A.19, 2B.22
    ///   Report 1/2/3 (MPRN)         → 2A.17, 2B.20
    ///   2B.21 Corrective (COMR)     → 2A.18, 2B.21
    /// </summary>
    /// <param name="row">Ligne brute Excel avec Cells normalisés et SheetName renseigné.</param>
    /// <param name="ingestionFileId">FK vers ingestion_files — traçabilité obligatoire.</param>
    /// <param name="reportingPeriod">Premier jour du mois de reporting.</param>
    /// <param name="sourceFileName">
    ///   Nom du fichier Excel source (ex: "MOD520A__PAF_Reports_Apr26_Non Anonymised.xlsx").
    ///   Utilisé par ReportCodeResolver pour déterminer report_code et euc_code.
    ///   Passé depuis PersistFilesHandler via result.FileName.
    /// </param>
    /// <param name="uploadedBy">Identifiant de l'auteur de l'import (par défaut "SYSTEM").</param>
    public static IEnumerable<MetricValue> MapToMetricValues(
        RawDataRow row,
        Guid ingestionFileId,
        DateOnly reportingPeriod,
        string sourceFileName,
        string uploadedBy = "SYSTEM")
    {
        var ssc = GetCell(row, "shippershortcode", "ssc", "code") ?? string.Empty;
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Résolution du ReportCode et EucCode ─────────────────────────────
        // ReportCodeResolver détermine le code PARR ("2A.1", "2B.11a", etc.) en
        // combinant le préfixe du nom de fichier et le nom de l'onglet Excel.
        var reportCode = ReportCodeResolver.Resolve(sourceFileName, row.SheetName);
        var eucCode    = GetCell(row, "euccode", "euc", "eucband", "band");

        // First pass: use aliases defined in MetricColumns, or directly the canonical metricKey if parser used it as cell key
        foreach (var (aliases, metricKey) in MetricColumns)
        {
            string? raw = GetCell(row, aliases);
            if (raw is null)
            {
                // Some parsers may write the canonical metric key as the column name
                if (row.Cells.TryGetValue(metricKey, out var v) && !string.IsNullOrWhiteSpace(v))
                    raw = v;
            }
            if (raw is null) continue;

            var value = ParseDecimal(raw);
            if (value is null) continue;

            emitted.Add(metricKey);

            var productClassCell = GetCell(row, "productclass", "product_class", "pc");
            var productClass = NormalizeProductClass(productClassCell);

            yield return new MetricValue
            {
                Id               = Guid.NewGuid(),
                IngestionFileId  = ingestionFileId,
                ShipperShortCode = ssc,
                MetricKey        = metricKey,
                Value            = value.Value,
                ProductClassCode = productClass,
                ReportingPeriod  = reportingPeriod,
                ReportCode       = reportCode,  // ex: "2A.1", "2B.11a" — résolu par ReportCodeResolver
                EucCode          = eucCode,     // renseigné pour EUC09 (2A.11a/b, 2B.14a/b)
                CreatedBy        = uploadedBy
            };
        }

        // Second pass: any remaining cells that look like canonical metric keys but weren't covered in MetricColumns
        var knownMetricKeys = new HashSet<string>(MetricColumns.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in row.Cells)
        {
            var key = kv.Key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (string.Equals(key, "shippershortcode", StringComparison.OrdinalIgnoreCase)) continue;
            if (emitted.Contains(key)) continue;

            if (knownMetricKeys.Contains(key))
            {
                var raw = kv.Value;
                var value = raw is null ? null : ParseDecimal(raw);
                if (value is null) continue;

                var productClassCell2 = GetCell(row, "productclass", "product_class", "pc");
                var productClass2 = NormalizeProductClass(productClassCell2);

                yield return new MetricValue
                {
                    Id               = Guid.NewGuid(),
                    IngestionFileId  = ingestionFileId,
                    ShipperShortCode = ssc,
                    MetricKey        = key,
                    Value            = value.Value,
                    ProductClassCode = productClass2,
                    ReportingPeriod  = reportingPeriod,
                    ReportCode       = reportCode,
                    EucCode          = eucCode,
                    CreatedBy        = uploadedBy
                };
            }
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

    private static string? NormalizeProductClass(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToUpperInvariant();
        if (s == "PC1" || s == "PC2" || s == "PC3" || s == "PC4") return s;
        if (s.Contains("CLASS 1") || s.Contains("CLASS1") || s.Contains("1")) return "PC1";
        if (s.Contains("CLASS 2") || s.Contains("CLASS2") || s.Contains("2")) return "PC2";
        if (s.Contains("CLASS 3") || s.Contains("CLASS3") || s.Contains("3")) return "PC3";
        if (s.Contains("CLASS 4") || s.Contains("CLASS4") || s.Contains("4")) return "PC4";
        return null;
    }
}