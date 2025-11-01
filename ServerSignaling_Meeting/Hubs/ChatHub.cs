using Microsoft.AspNetCore.SignalR;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalingServer.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;
        private readonly ChatMessageRepository _chatMessageRepository;

        public ChatHub(ILogger<ChatHub> logger, ChatMessageRepository chatMessageRepository)
        {
            _logger = logger;
            _chatMessageRepository = chatMessageRepository;
        }

        public async Task SendMessage(Guid roomId, Guid userId, string message)
        {
            if (message == null)
            {
                _logger.LogWarning("Attempted to save null message");
                throw new ArgumentNullException(nameof(message));
            }


        }
    }
}
