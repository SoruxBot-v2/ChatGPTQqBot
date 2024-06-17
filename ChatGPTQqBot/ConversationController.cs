using ChatGPTQQBot.Model;
using RestSharp;
using SoruxBot.SDK.Attribute;
using SoruxBot.SDK.Model.Attribute;
using SoruxBot.SDK.Model.Message;
using SoruxBot.SDK.Plugins.Basic;
using SoruxBot.SDK.Plugins.Model;
using SoruxBot.SDK.Plugins.Service;
using SoruxBot.SDK.QQ;
using Newtonsoft.Json.Linq;

namespace ChatGPTQQBot;

public class ConversationController(ILoggerService loggerService, ICommonApi bot, IPluginsDataStorage dataStorage) : PluginController
{
    private readonly ICommonApi _bot = bot;
    private readonly IPluginsDataStorage _dataStorage = dataStorage;
    private ILoggerService _logger = loggerService;
    private RestClient _client = new("https://service.soruxgpt.com/api/api/v1/chat/completions");
	private string _bearerToken = "sk-H9KV0GXczORw0OZRCvrRCptR28lEqXwOJtQgoMFKYvZmhUiU";
	private const int _maxConversationCount = 10;

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
            var msg = new Task<MessageContext?>(()=> bot.QqReadNextGroupMessageAsync(context.TriggerId, context.TriggerPlatformId).Result).Result;
			if(msg != null)
			{
				var userMessage = new Message("user",
					msg.MessageChain!.Messages.Select(p => p.ToPreviewText()).Aggregate((t1, t2) => t1 + t2));
				// TODO 改成优雅的停止方式
				if (userMessage.Content == "#gpt stop") break;

				var postRequest = new RestRequest(string.Empty, Method.Post);
				postRequest.AddHeader("Authorization", $"Bearer {_bearerToken}");
				postRequest.AddJsonBody(
						new RequestBody(model, conversation.Messages.Append(userMessage).ToList())
					);
				var getResponse = _client.Execute(postRequest);
				if(getResponse.IsSuccessful)
				{
					_logger.Info("ChatGPTQQBot-Chat", "Successfully get response");
					var responseContent = JObject.Parse(getResponse.Content!);
					conversation.Messages.Add(userMessage);
					conversation.Messages.Add(new Message("system", getResponse.Content!));
					if (conversation.Messages.Count > 2 * _maxConversationCount)
					{
						_bot.QqSendGroupMessage(QqMessageBuilder
						.GroupMessage(context.TriggerPlatformId)
						.Text($"对话数量已超上限({_maxConversationCount}条)，对话自动终止")
						.Build(),
						context.BotAccount);
						break;
					}
					else
					{
						_bot.QqSendGroupMessage(QqMessageBuilder
							.GroupMessage(context.TriggerPlatformId)
							.Text((string)responseContent["choices"]![0]!["message"]!["content"]!)
							.Build(),
							context.BotAccount);
						_logger.Info("ChatGPRQQBot-Chat", "Token usage:" + responseContent["usage"]);
					}
				}
				else
				{
					_logger.Info("ChatGPTQQBot-Chat", $"Failed to get response, error code: {getResponse.StatusCode}");
					_bot.QqSendGroupMessage(QqMessageBuilder
						.GroupMessage(context.TriggerPlatformId)
						.Text("获取回复失败，错误码：" + getResponse.StatusCode)
						.Build(),
						context.BotAccount);
				}
			}
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