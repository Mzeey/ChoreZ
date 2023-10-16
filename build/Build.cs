using System;
using System.Linq;
using Microsoft.VisualBasic;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Octokit;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;


[GitHubActions(
    "build-and-test",
    GitHubActionsImage.UbuntuLatest,
    OnPullRequestBranches = new[] { "master", "main", "staging", "development" },
    InvokedTargets = new[] { nameof(RunTests) })]
[GitHubActions(
    "enforce-development-to-staging",
    GitHubActionsImage.UbuntuLatest,
    OnPullRequestBranches = new[] { "test_staging"},
    InvokedTargets = new[] { nameof(EnforceDevelopmentToStaging)})]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {

        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore();
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild();
        });

    Target RunUnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Logger.Normal("Running Unit Test");
            DotNetTest(_ =>
                _.SetProjectFile(RootDirectory / "ChoreZ.UnitTests")
                .EnableNoBuild()
                .EnableNoRestore()
            );
        });

    Target RunIntegrationTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Logger.Normal("Running Integration Test");
            DotNetTest(_ =>
                _.SetProjectFile(RootDirectory / "ChoreZ.IntegrationTests")
                .EnableNoBuild()
                .EnableNoRestore()
            );
        });

    Target RunTests => _ => _
        .DependsOn(RunIntegrationTest, RunUnitTest);

    [GitRepository] GitRepository Repository;

    Target EnforceDevelopmentToStaging => _ => _
    .Executes(() =>
    {
        var sourceBranch = Repository.Branch;

        if (sourceBranch != "test_development")
        {
            throw new Exception("Merging into the staging branch is only allowed from the development branch.");
        }
    });
}
