using System.Linq;
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

            var integrationMachine = CreateMachine(repository, integrationEnvironment, "Integration");
            var uatMachine = CreateMachine(repository, uatEnvironment, "UAT");
            var productionMachine = CreateMachine(repository, productionEnvironment, "Production");

            var environments = new[] {integrationEnvironment, uatEnvironment, productionEnvironment};
        
            var projectGroup = CreateProjectGroup(repository);

            var lifecycle = repository.Lifecycles.FindOne(l => l.Name == "Default Lifecycle");

            var project = CreateProject(repository, projectGroup, lifecycle, "SimpleTalk");

            CreateDeploymentProcess(repository, project);

            CreateDashboard(repository, environments, project);

            var template = repository.Client.Get<ActionTemplateResource>(repository.Client.RootDocument.Links["ActionTemplates"]);

            string name;
            string scriptBody;

            using (var webClient = new System.Net.WebClient())
            {
                var json = webClient.DownloadString("https://raw.githubusercontent.com/OctopusDeploy/Library/master/step-templates/redgate-create-database-release.json");
                var o = JObject.Parse(json);
                name = (string) o["Name"];
                var properties = o["Properties"];
                scriptBody = (string)properties["Octopus.Action.Script.ScriptBody"];
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

        private static void CreateDeploymentProcess(OctopusRepository repository, ProjectResource project)
        {
            var deploymentProcess = repository.DeploymentProcesses.Get(project.DeploymentProcessId);

            var deploymentStepResource = new DeploymentStepResource
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
            };

            deploymentProcess.Steps.Add(deploymentStepResource);

            repository.DeploymentProcesses.Modify(deploymentProcess);
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
