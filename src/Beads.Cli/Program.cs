using System.CommandLine;
using Beads.Cli.Commands;

var root = new RootCommand("beads — local-first issue tracker");
Globals.AddTo(root);
IssueCommands.Register(root);
FilterCommands.Register(root);
DepCommands.Register(root);
LabelCommands.Register(root);
EpicCommands.Register(root);
CommentCommands.Register(root);
WorkflowCommands.Register(root);
SyncCommands.Register(root);
DiagCommands.Register(root);
ProjectCommands.Register(root);
var config = new CommandLineConfiguration(root);
return await config.InvokeAsync(args);
