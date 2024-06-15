using ChatGPTQqBot.Model;
using RestSharp;
using SoruxBot.SDK.Attribute;
using SoruxBot.SDK.Model.Attribute;
using SoruxBot.SDK.Model.Message;
using SoruxBot.SDK.Plugins.Basic;
using SoruxBot.SDK.Plugins.Model;
using SoruxBot.SDK.Plugins.Service;
using SoruxBot.SDK.QQ;

namespace ChatGPTQqBot;

public class ConversationController(ILoggerService loggerService, ICommonApi bot, IPluginsDataStorage dataStorage) : PluginController
{
    private readonly ICommonApi _bot = bot;
    private readonly IPluginsDataStorage _dataStorage = dataStorage;
    private ILoggerService _logger = loggerService;
    private RestClient _client = new("https://service.soruxgpt.com/api/api/v1/chat/completions");
    
    [MessageEvent(MessageType.PrivateMessage)]
    [Command(CommandPrefixType.Single, "gpt <action> <param>")]
    public PluginFlag SetUserToken(MessageContext context, string action, string param)
    {
        switch (action)
        {
            case "set":
            {
                _dataStorage.AddStringSettings("chatgpt", "token:"+context.TriggerId, param);
                var chain = QqMessageBuilder
                    .PrivateMessage(context.TriggerId)
                    .Text("设置成功！")
                    .Build();
                
                _bot.QqSendGroupMessage(chain, context.BotAccount);
                break;
            }
            default:
            {
                var chain = QqMessageBuilder
                    .PrivateMessage(context.TriggerId)
                    .Text("你好，请输入正确的指令：gpt set <token>")
                    .Build();
                
                _bot.QqSendGroupMessage(chain, context.BotAccount);
                break;
            }
        }
        return PluginFlag.MsgIntercepted;
    }

    [MessageEvent(MessageType.GroupMessage)]
    [Command(CommandPrefixType.Single, "gpt <action> [model]")]
    public PluginFlag Chat(MessageContext context, string action, string? model)
    {
        if (action != "start")
        {
            _bot.QqSendGroupMessage(
                QqMessageBuilder
                    .GroupMessage(context.TriggerPlatformId)
                    .Text("你好，ChatGPT AI 在未对话时只能运行输入启动对话指令：#gpt start")
                    .Build(), 
                    context.BotAccount
                );
            return PluginFlag.MsgIntercepted;
        }
        
        var token = _dataStorage.GetStringSettings("chatgpt", "token:"+context.TriggerId);
        
        if (string.IsNullOrEmpty(token))
        {
            _bot.QqSendGroupMessage(
                QqMessageBuilder
                    .GroupMessage(context.TriggerPlatformId)
                    .Text("你好，请先私聊机器人，通过指令'#gpt set <token>'设置你的SoruxGPT Token。")
                    .Build(), 
                context.BotAccount
            );
            return PluginFlag.MsgIntercepted;
        }

        if (string.IsNullOrEmpty(model))
        {
            model = "gpt-4o";
        }

        // 开始对话
        var loop = true;
        var conversation = new Conversation(model);
        
        _bot.QqSendGroupMessage(
            QqMessageBuilder
                .GroupMessage(context.TriggerPlatformId)
                .Text("您好，有什么是 SoruxBot 可以帮助到您的吗？")
                .Build(), 
            context.BotAccount
        );
        
        do
        {
            bot.QqReadNextGroupMessageAsync(
                context.TriggerId,
                context.TriggerPlatformId,
                ctx =>
                {
                    var req = GetSoruxGptRequest(token)
                        .AddJsonBody(conversation);
                    
                    return PluginFlag.MsgIntercepted;
                },
                ctx =>
                {
                    _bot.QqSendGroupMessage(
                        QqMessageBuilder
                            .GroupMessage(context.TriggerPlatformId)
                            .Text("你好，你在超时时间内未输入任何内容，对话已结束。")
                            .Build(), 
                        context.BotAccount
                    );
                    loop = false;
                    return PluginFlag.MsgIntercepted;
                }
                );
        } while (loop);
        return PluginFlag.MsgIntercepted;
    }

    private RestRequest GetSoruxGptRequest(string token)
    {
        var req = new RestRequest()
            .AddHeader("Authorization", $"Bearer {token}")
            .AddHeader("Content-Type", "application/json");
        return req;
    }
}