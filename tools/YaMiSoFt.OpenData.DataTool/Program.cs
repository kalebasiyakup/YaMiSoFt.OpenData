using YaMiSoFt.OpenData.DataTool;

// Manual regeneration entry point. The tool is deliberately NOT wired into the build:
// its output is committed to data/ so that builds stay deterministic and offline
// (PLAN.md 4). Run it by hand when an upstream source changes, then review the diff.
return await DataToolCommandLine.RunAsync(args).ConfigureAwait(false);
