using System;
using System.Linq;
using System.Net;
using System.Threading;
using Newtonsoft.Json.Linq;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using Octopus.Platform.Variables;

namespace ProvisionOctopusDeploy
{
    class Program
    {
        static void Main(string[] args)
        {
            var endpoint = new OctopusServerEndpoint("http://localhost/");
            var repository = new OctopusRepository(endpoint);

            repository.Users.SignIn(new LoginCommand {Username = "Redgate", Password = "Redg@te1"});

            var integrationEnvironment = CreateEnvironment(repository, "Integration");
            var uatEnvironment = CreateEnvironment(repository, "UAT");
            var productionEnvironment = CreateEnvironment(repository, "Production");

            var environments = new[] { integrationEnvironment, uatEnvironment, productionEnvironment };

            var integrationMachine = CreateMachine(repository, integrationEnvironment, "Integration");
            var uatMachine = CreateMachine(repository, uatEnvironment, "UAT");
            var productionMachine = CreateMachine(repository, productionEnvironment, "Production");
        
            var projectGroup = CreateProjectGroup(repository);

            var lifecycle = repository.Lifecycles.FindOne(l => l.Name == "Default Lifecycle");

            var project = CreateProject(repository, projectGroup, lifecycle, "SimpleTalk");

            FeedResource feed = null;

            try
            {
                feed = repository.Feeds.Create(new FeedResource { Name = "TeamCity", FeedUri = "http://localhost/" });
            }
            catch (Exception)
            {
                feed = repository.Feeds.FindByName("TeamCity");
            }

            CreateDeploymentProcess(repository, project, feed);

            CreateDashboard(repository, environments, project);

            CreateActionTemplate(repository);
        }

        private static void CreateActionTemplate(IOctopusRepository repository)
        {
            string name;
            string scriptBody;

            using (var webClient = new WebClient())
            {
                var json =
                    webClient.DownloadString(
                        "https://raw.githubusercontent.com/OctopusDeploy/Library/master/step-templates/redgate-create-database-release.json");
                var o = JObject.Parse(json);
                name = (string) o["Name"];
                var properties = o["Properties"];
                scriptBody = (string) properties["Octopus.Action.Script.ScriptBody"];
            }

            var actionTemplateResource = new ActionTemplateResource
            {
                Name = name,
                ActionType = "Octopus.Script",
                Properties =
                {
                    {SpecialVariables.Action.Script.ScriptBody, scriptBody}
                }
            };

            repository.Client.Post(repository.Client.RootDocument.Links["ActionTemplates"], actionTemplateResource);
        }

        private static void CreateDeploymentProcess(IOctopusRepository repository, ProjectResource project, FeedResource feed)
        {
            var deploymentProcess = repository.DeploymentProcesses.Get(project.DeploymentProcessId);

            CreateVariable(repository, project, "serverInstance", @".\SQL2012");
            CreateVariable(repository, project, "password", "Redg@te1", true);

            deploymentProcess.Steps.Add(new DeploymentStepResource
            {
                Name = "Download and extract database package",
                Condition = DeploymentStepCondition.Success,
                Properties =
                {
                    {"Octopus.Action.TargetRoles", "database"}
                },
                Actions =
                {
                    new DeploymentActionResource
                    {
                        ActionType = "Octopus.TentaclePackage",
                        Name = "Database package",
                        Properties =
                        {
                            {SpecialVariables.Action.Package.NuGetPackageId, "SimpleTalkDatabase"},
                            {SpecialVariables.Action.Package.NuGetFeedId, feed.Id},
                            {SpecialVariables.Action.Package.AutomaticallyRunConfigurationTransformationFiles, "False"},
                            {SpecialVariables.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, "False"}
                        }
                    }
                }
            });

            deploymentProcess.Steps.Add(new DeploymentStepResource
            {
                Name = "Step A",
                Condition = DeploymentStepCondition.Success,
                Properties =
                {
                    {"Octopus.Action.TargetRoles", "database"}
                },
                Actions =
                {
                    new DeploymentActionResource
                    {
                        ActionType = "Octopus.Script",
                        Name = "Say Hello",
                        Properties =
                        {
                            {SpecialVariables.Action.Script.ScriptBody, "Write-Host 'Hello'"}
                        }
                    }
                }
            });

            repository.DeploymentProcesses.Modify(deploymentProcess);
        }

        private static void CreateVariable(IOctopusRepository repository, ProjectResource project, string name, string value, bool isSensitive = false)
        {
            var vs = repository.VariableSets.Get(project.VariableSetId);

            var v = vs.Variables.FirstOrDefault(x => x.Name == name);
            if (v != null)
                v.Value = value;
            else
                vs.Variables.Add(new VariableResource {Name = name, Value = value, IsSensitive = isSensitive});

            repository.VariableSets.Modify(vs);
        }

        private static DashboardConfigurationResource CreateDashboard(OctopusRepository repository, IResource[] environments,
            IResource project)
        {
            var dashboardConfiguration = repository.DashboardConfigurations.GetDashboardConfiguration();

            dashboardConfiguration.IncludedEnvironmentIds = new ReferenceCollection(environments.Select(e => e.Id));
            dashboardConfiguration.IncludedProjectIds = new ReferenceCollection(project.Id);
            return dashboardConfiguration;
        }

        private static IResource CreateMachine(IOctopusRepository repository, IResource productionEnvironment, string name)
        {
            var machine = repository.Machines.Discover("localhost");

            machine.Name = name;
            machine.EnvironmentIds = new ReferenceCollection(productionEnvironment.Id);
            machine.Roles.Add("database");

            return repository.Machines.FindByName(name) ?? repository.Machines.Create(machine);
        }

        private static IResource CreateEnvironment(IOctopusRepository repository, string name)
        {
            return repository.Environments.FindByName(name) ??
                   repository.Environments.Create(new EnvironmentResource {Name = name});
        }

        private static ProjectResource CreateProject(IOctopusRepository repository, IResource projectGroup,
            IResource lifecycle, string name)
        {
            var project = repository.Projects.FindByName(name);

            if (project != null)
            {
                repository.Projects.Delete(project);
                Thread.Sleep(500);
            }

            var projectResource = new ProjectResource
            {
                Name = name,
                ProjectGroupId = projectGroup.Id,
                LifecycleId = lifecycle.Id
            };

            return repository.Projects.Create(projectResource);
        }

        private static ProjectGroupResource CreateProjectGroup(IOctopusRepository repository)
        {
            var projectGroupResource = new ProjectGroupResource
            {
                Name = "SimpleTalk",
                Description = "Deploy SimpleTalk application and database schema"
            };

            var projectGroup = repository.ProjectGroups.FindByName("SimpleTalk") ??
                               repository.ProjectGroups.Create(projectGroupResource);
            return projectGroup;
        }
    }
}
