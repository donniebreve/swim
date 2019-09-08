using Common;
using Common.Configuration;
using Common.Configuration.Json;
using Common.Migration;
using Common.Serialization.Json;
using Common.Validation;
using Logging;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WiMigrator
{
    public class CommandLine
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<CommandLine>();

        private CommandLineApplication commandLineApplication;
        private string[] args;

        public CommandLine(params string[] args)
        {
            InitCommandLine(args);
        }

        private void InitCommandLine(params string[] args)
        {
            commandLineApplication = new CommandLineApplication(throwOnUnexpectedArg: true);
            this.args = args;
            ConfigureCommandLineParserWithOptions();
        }

        private void ConfigureCommandLineParserWithOptions()
        {
            commandLineApplication.HelpOption("-? | -h | --help");
            commandLineApplication.FullName = "Work Item Migrator tool assists with copying work items" +
                " from one Visual Studio Team Services account to another.";

            CommandOption validate = commandLineApplication.Option(
                "--validate <configurationfilename>",
                "Readiness check of the work item migration" +
                " based on the configuration settings",
                CommandOptionType.SingleValue
                );

            CommandOption migrate = commandLineApplication.Option(
                "--migrate <configurationfilename>",
                "Migrate the work items based" +
                " on the configuration settings",
                CommandOptionType.SingleValue
                );

            commandLineApplication.OnExecute(async () =>
            {
                if (validate.HasValue())
                {
                    await ExecuteValidation(validate);
                }
                else if (migrate.HasValue())
                {
                    await ExecuteMigration(migrate);
                }
                else
                {
                    commandLineApplication.ShowHelp();
                }

                return 0;
            });
        }

        private async Task ExecuteValidation(CommandOption validate)
        {
            bool showedHelp = false;
            IConfiguration configuration = null;
            try
            {
                configuration = ConfigurationReader.LoadFromFile<Configuration, JsonSerializer>(validate.Value());
                var context = new ValidationContext(configuration);
                using (var heartbeat = new ValidationHeartbeatLogger(context, configuration.HeartbeatFrequencyInSeconds))
                {
                    await new Validator(context).Validate();
                    heartbeat.Beat();
                }
            }
            catch (CommandParsingException e)
            {
                Logger.LogError(LogDestination.All, e, "Invalid command line option(s):");
                commandLineApplication.ShowHelp();
                showedHelp = true;
            }
            catch (Exception e) when (e is ValidationException)
            {
                Logger.LogError(LogDestination.All, e, "Validation error:");
            }
            catch (Exception e)
            {
                Logger.LogError(LogDestination.All, e, "Unexpected error:");
            }
            finally
            {
                if (!showedHelp && configuration != null)
                {
                    SendSummaryEmail(configuration);
                }
            }
        }

        private async Task ExecuteMigration(CommandOption migrate)
        {
            bool showedHelp = false;
            IConfiguration configuration = null;
            try
            {
                configuration = ConfigurationReader.LoadFromFile<Configuration, JsonSerializer>(migrate.Value());
                var validatorContext = new ValidationContext(configuration);
                //using (var heartbeat = new ValidationHeartbeatLogger(validatorContext, configuration.HeartbeatFrequencyInSeconds))
                //{
                //    await new Validator(validatorContext).Validate();
                //    heartbeat.Beat();
                //}
                await new Validator(validatorContext).Validate();

                //TODO: Create a common method to take the validator context and created a migration context
                var context = new MigrationContext(configuration);

                context.WorkItemMigrationStateDictionary = validatorContext.WorkItemMigrationStateDictionary;
                context.WorkItemTypes = validatorContext.TargetTypesAndFields;
                context.IdentityFields = validatorContext.IdentityFields;
                context.TargetAreaPaths = validatorContext.TargetAreaPaths;
                context.TargetIterationPaths = validatorContext.TargetIterationPaths;
                context.TargetIdToSourceHyperlinkAttributeId = validatorContext.TargetIdToSourceHyperlinkAttributeId;
                context.ValidatedWorkItemLinkRelationTypes = validatorContext.ValidatedWorkItemLinkRelationTypes;
                context.RemoteLinkRelationTypes = validatorContext.RemoteLinkRelationTypes;
                context.SourceFields = validatorContext.SourceFields;
                context.FieldsThatRequireSourceProjectToBeReplacedWithTargetProject = validatorContext.FieldsThatRequireSourceProjectToBeReplacedWithTargetProject;

                //using (var heartbeat = new MigrationHeartbeatLogger(context, context.Configuration.HeartbeatFrequencyInSeconds))
                //{
                //    await new Migrator(context).Migrate();
                //    heartbeat.Beat();
                //}
                await new Migrator(context).Migrate();
            }
            catch (CommandParsingException e)
            {
                Logger.LogError(LogDestination.All, e, "Invalid command line option(s):");
                commandLineApplication.ShowHelp();
                showedHelp = true;
            }
            catch (Exception e) when (e is ValidationException)
            {
                Logger.LogError(LogDestination.All, e, "Validation error:");
            }
            catch (Exception e) when (e is MigrationException)
            {
                Logger.LogError(LogDestination.All, e, "Migration error:");
            }
            catch (Exception e)
            {
                Logger.LogError(LogDestination.All, e, $"Unexpected error: {e}");
            }
            finally
            {
                if (!showedHelp && configuration != null)
                {
                    SendSummaryEmail(configuration);
                }
            }
        }

        /// <summary>
        /// Run the WiMigrator application
        /// </summary>
        public void Run()
        {
            commandLineApplication.Execute(args);
        }

        private void SendSummaryEmail(IConfiguration configuration)
        {
            string logSummaryText = MigratorLogging.GetLogSummaryText();
            Emailer emailer = new Emailer();
            emailer.SendEmail(configuration, logSummaryText);
        }
    }
}
