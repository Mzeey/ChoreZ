using System;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
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
    OnPullRequestBranches = new[] { "staging"},
    InvokedTargets = new[] { nameof(EnforceDevelopmentToStaging)})]

[GitHubActions(
    "enforce-staging-to-master",
    GitHubActionsImage.UbuntuLatest,
    OnPullRequestBranches = new[] { "master" },
    InvokedTargets = new[] { nameof(EnforceStagingToMaster) })]
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
    .DependsOn(RunTests)
    .Executes(() =>
    {

        Logger.Info("Enforcing that the 'development' branch can merge into 'staging'.");
        var sourceBranch = getSourceBranch();
        var targetBranch = getTargetBranch();

        if (sourceBranch != "development")
        {
            throw new Exception($"Merging into the `{targetBranch}` branch is only allowed from the `development` branch.");
        }

        Logger.Info("The merge is permitted.");
    });

    Target EnforceStagingToMaster => _ => _
    .DependsOn(RunTests)
    .Executes(() =>
    {
        Logger.Info("Enforcing that the 'staging' branch can merge into 'master'.");
        var sourceBranch = getSourceBranch();
        var targetBranch = getTargetBranch();

        if (sourceBranch != "staging")
        {
            throw new Exception($"Merging into the `{targetBranch}` branch is only allowed from the `development` branch.");
        }
        Logger.Info("The merge is permitted.");
    });


    private string getSourceBranch()
    {
        var sourceBranch = Repository.Branch;
        if (sourceBranch.StartsWith("refs/pull/"))
        {
            // Use jq to parse the JSON payload provided by GitHub
            var prJson = File.ReadAllText(Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH"));
            sourceBranch = JObject.Parse(prJson)["pull_request"]["head"]["ref"].ToString();
        }

        return sourceBranch;
    }

    private string getTargetBranch()
    {
        return Environment.GetEnvironmentVariable("GITHUB_BASE_REF");
    }
}
