using PCLCS;

namespace PCL.Core.Java;

public sealed class JavaRequirementContext {

    public string VersionDisplayName { get; set; } = "";
    public bool VersionValid { get; set; }
    public Version VanillaVersion { get; set; } = new(9999, 0, 0);
    public bool HasOptiFine { get; set; }
    public ModLoaders ModLoaderFlags { get; set; } = ModLoaders.None;
    public IReadOnlyDictionary<ModLoaders, string> ModLoaderVersions { get; set; } = new Dictionary<ModLoaders, string>();
    public DateTime ReleaseTime { get; set; } = new(1970, 1, 1);
    public int? MojangJavaMajor { get; set; }
    public string? MojangJavaComponent { get; set; }
    public int? JsonJavaMajor { get; set; }
    public string? JsonJavaComponent { get; set; }
    public bool IsNideLogin { get; set; }
    public bool HasJavaVersionOverride { get; set; }
    public string? JavaVersionOverride { get; set; }

}

public sealed class JavaRequirementResult {

    public ValueRange<Version> Range { get; }
    public string? RecommendedComponent { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> ConflictWarnings { get; }
    public IReadOnlyList<string> InfoMessages { get; }

    public JavaRequirementResult(
        ValueRange<Version> range,
        string? recommendedComponent,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? conflictWarnings = null,
        IReadOnlyList<string>? infoMessages = null) {
        Range = range;
        RecommendedComponent = recommendedComponent;
        Warnings = warnings ?? Array.Empty<string>();
        ConflictWarnings = conflictWarnings ?? Array.Empty<string>();
        InfoMessages = infoMessages ?? Array.Empty<string>();
    }

}

/// <summary>
/// 根据 Minecraft 版本、Mod Loader、OptiFine 与登录方式推导 Java 版本要求。
/// </summary>
public static class JavaRequirementResolver {

    public static JavaRequirementResult Resolve(JavaRequirementContext context) {
        var warnings = new List<string>();
        var conflicts = new List<string>();
        var infoMessages = new List<string>();

        if (context.HasJavaVersionOverride && !string.IsNullOrWhiteSpace(context.JavaVersionOverride)) {
            try {
                var overrideRange = ValueRange<Version>.FromString(
                    context.JavaVersionOverride!,
                    StringUtils.ParseVersionWithDefaults);
                return new JavaRequirementResult(overrideRange, null, warnings, conflicts, infoMessages);
            } catch (Exception) {
                warnings.Add($"版本设置中指定的 Java 版本范围有误（{context.JavaVersionOverride}），这可能是因为首次修改此设置，尚未初始化");
            }
        }

        var javaRange = ValueRange<Version>.All();

        void AddConstraint(ValueRange<Version> constraint) {
            javaRange = javaRange.Intersect(constraint);
            if (!javaRange.IsEmpty()) return;
            conflicts.Add($"Java 版本要求冲突，若启动失败请在版本设置手动指定 Java：当前追加的要求为 {constraint}，游戏版本为 {context.VersionDisplayName}");
            javaRange = constraint;
        }

        var vanilla = context.VanillaVersion;

        // 原版 26-：通过版本号判断
        if ((!context.VersionValid && context.ReleaseTime >= new DateTime(2024, 4, 2))
            || (context.VersionValid && vanilla.CompareTo(new Version(20, 0, 5)) >= 0)) {
            // 1.20.5+（24w14a+）：至少 Java 21
            AddConstraint(ValueRange<Version>.AtLeast(new Version(21, 0)));
        } else if ((!context.VersionValid && context.ReleaseTime >= new DateTime(2021, 11, 16))
                   || (context.VersionValid && vanilla.Major >= 18)) {
            // 1.18 pre2+：至少 Java 17
            AddConstraint(ValueRange<Version>.AtLeast(new Version(17, 0)));
        } else if ((!context.VersionValid && context.ReleaseTime >= new DateTime(2021, 6, 11))
                   || (context.VersionValid && vanilla.Major >= 17)) {
            // 1.17+（21w19a+）：至少 Java 16
            AddConstraint(ValueRange<Version>.AtLeast(new Version(16, 0)));
        } else if (context.ReleaseTime.Year >= 2017) {
            // 1.12+：至少 Java 8
            AddConstraint(ValueRange<Version>.AtLeast(new Version(8, 0)));
        } else if (context.ReleaseTime <= new DateTime(2013, 5, 1) && context.ReleaseTime.Year >= 2001) {
            // 1.5.2-：最高 Java 8
            AddConstraint(ValueRange<Version>.LessThan(new Version(9, 0)));
        }

        // 原版 26+：获取 Mojang 要求的 Java 版本
        var recommendedCode = context.MojangJavaMajor ?? context.JsonJavaMajor ?? 0;
        string? recommendedComponent = null;
        if (recommendedCode >= 22) {
            infoMessages.Add("Mojang 要求至少使用 Java " + recommendedCode);
            AddConstraint(ValueRange<Version>.AtLeast(new Version(recommendedCode, 0)));
            recommendedComponent = context.MojangJavaComponent ?? context.JsonJavaComponent;
            if (string.IsNullOrEmpty(recommendedComponent)) recommendedComponent = null;
        }

        // OptiFine 检测
        if (context.HasOptiFine && context.VersionValid) {
            if (vanilla.Major < 7) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(9, 0)));
            } else if (vanilla.Major >= 8 && vanilla.Major < 12) {
                AddConstraint(ValueRange<Version>.ClosedOpen(new Version(8, 0), new Version(9, 0)));
            } else if (vanilla.Major == 12) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(9, 0)));
            }
        }

        // Forge 检测
        if (context.ModLoaderFlags.HasFlagF(ModLoaders.Forge)) {
            var forgeVersion = GetLoaderVersion(context, ModLoaders.Forge);
            if (vanilla.CompareTo(new Version(6, 0, 1)) >= 0 && vanilla.CompareTo(new Version(7, 0, 2)) <= 0) {
                AddConstraint(ValueRange<Version>.ClosedOpen(new Version(7, 0), new Version(8, 0)));
            } else if (vanilla.Major <= 12 || !context.VersionValid) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(9, 0)));
            } else if (vanilla.Major <= 14) {
                AddConstraint(ValueRange<Version>.ClosedOpen(new Version(8, 0), new Version(11, 0)));
            } else if (vanilla.Major == 15) {
                AddConstraint(ValueRange<Version>.ClosedOpen(new Version(8, 0), new Version(16, 0)));
            } else if (McUtils.CompareVersionGE(forgeVersion, "34.0.0") && McUtils.CompareVersionGE("36.2.25", forgeVersion)) {
                AddConstraint(ValueRange<Version>.AtMost(new Version(8, 0, 320)));
            } else if (McUtils.CompareVersionGE(forgeVersion, "36.2.26") && McUtils.CompareVersionGE("36.999999.999999", forgeVersion)) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(24, 0)));
            } else if (McUtils.CompareVersionGE(forgeVersion, "37.0.0") && McUtils.CompareVersionGE("37.0.79", forgeVersion)) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(17, 0)));
            } else if (vanilla.Major == 18 && context.HasOptiFine) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(19, 0)));
            } else if (McUtils.CompareVersionGE(forgeVersion, "45.0.21") && McUtils.CompareVersionGE("45.0.65", forgeVersion)) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(20, 0)));
            } else if (McUtils.CompareVersionGE(forgeVersion, "45.0.66") && McUtils.CompareVersionGE("47.4.8", forgeVersion)) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(22, 0)));
            }
        }

        // NeoForge 检测
        if (context.ModLoaderFlags.HasFlagF(ModLoaders.NeoForge)) {
            var neoForgeVersion = GetLoaderVersion(context, ModLoaders.NeoForge);
            if (vanilla.CompareTo(new Version(20, 0, 1)) == 0
                || (McUtils.CompareVersionGE("20.2.62-beta", neoForgeVersion)
                    && neoForgeVersion.IndexOf("25w14craftmine", StringComparison.Ordinal) < 0)) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(22, 0)));
            }
        }

        // Fabric 检测
        if (context.ModLoaderFlags.HasFlagF(ModLoaders.Fabric) && context.VersionValid) {
            var fabricVersion = GetLoaderVersion(context, ModLoaders.Fabric);
            if (vanilla.Major >= 15 && vanilla.Major <= 16) {
                AddConstraint(ValueRange<Version>.AtLeast(new Version(8, 0)));
            } else if (vanilla.Major >= 18) {
                AddConstraint(ValueRange<Version>.AtLeast(new Version(17, 0)));
            }
            if (fabricVersion != "未知版本" && McUtils.CompareVersion(fabricVersion, "0.17.0") < 0) {
                AddConstraint(ValueRange<Version>.LessThan(new Version(25, 0)));
            }
        }

        // LiteLoader 检测
        if (context.ModLoaderFlags.HasFlagF(ModLoaders.LiteLoader) && context.VersionValid) {
            AddConstraint(ValueRange<Version>.LessThan(new Version(9, 0)));
        }

        // 统一通行证检测
        if (context.IsNideLogin) {
            AddConstraint(ValueRange<Version>.AtLeast(new Version(8, 0, 141)));
        }

        return new JavaRequirementResult(javaRange, recommendedComponent, warnings, conflicts, infoMessages);
    }

    private static string GetLoaderVersion(JavaRequirementContext context, ModLoaders loader) {
        return context.ModLoaderVersions.TryGetValue(loader, out var version) ? version : "";
    }

}
