using SoruxBot.SDK.Plugins.Ability;
using SoruxBot.SDK.Plugins.Basic;

namespace ChatGPTQqBot;

public class Register : SoruxBotPlugin, ICommandPrefix
{
    public override string GetPluginName() => "ChatGPT AI Assistant";

    public override string GetPluginVersion() => "1.0.0";

    public override string GetPluginAuthorName() => "Open SoruxBot Plugin";

    public override string GetPluginDescription() => "SoruxBot Plugin for ChatGPT AI Assistant.";
    
    public string GetPluginPrefix() => "#";
}