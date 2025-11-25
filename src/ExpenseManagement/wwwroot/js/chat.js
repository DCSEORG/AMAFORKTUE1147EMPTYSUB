// Chat functionality
document.addEventListener('DOMContentLoaded', function() {
    const chatToggle = document.getElementById('chatToggle');
    const chatWindow = document.getElementById('chatWindow');
    const chatClose = document.getElementById('chatClose');
    const chatInput = document.getElementById('chatInput');
    const chatSend = document.getElementById('chatSend');
    const chatMessages = document.getElementById('chatMessages');

    let chatHistory = [];

    // Toggle chat window
    chatToggle.addEventListener('click', function() {
        chatWindow.classList.toggle('d-none');
        if (!chatWindow.classList.contains('d-none')) {
            chatInput.focus();
        }
    });

    chatClose.addEventListener('click', function() {
        chatWindow.classList.add('d-none');
    });

    // Send message
    async function sendMessage() {
        const message = chatInput.value.trim();
        if (!message) return;

        // Add user message to UI
        addMessage(message, 'user');
        chatInput.value = '';

        // Add to history
        chatHistory.push({ role: 'user', content: message });

        // Show typing indicator
        const typingId = showTyping();

        try {
            const response = await fetch('/api/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: message,
                    history: chatHistory.slice(-10) // Last 10 messages for context
                })
            });

            const data = await response.json();
            
            // Remove typing indicator
            removeTyping(typingId);

            // Add assistant response
            addMessage(data.response, 'assistant');
            
            // Add to history
            chatHistory.push({ role: 'assistant', content: data.response });

        } catch (error) {
            removeTyping(typingId);
            addMessage('Sorry, something went wrong. Please try again.', 'assistant');
        }
    }

    chatSend.addEventListener('click', sendMessage);
    chatInput.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            sendMessage();
        }
    });

    function addMessage(text, role) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `chat-message ${role}`;
        
        // Format the message content
        const formattedText = formatMessage(text);
        messageDiv.innerHTML = formattedText;
        
        chatMessages.appendChild(messageDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function formatMessage(text) {
        // First escape HTML to prevent XSS
        let escaped = text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');

        // Then apply formatting

        // Bold text: **text** -> <strong>text</strong>
        escaped = escaped.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

        // Process lines for lists
        const lines = escaped.split('\n');
        let result = [];
        let inOrderedList = false;
        let inUnorderedList = false;

        for (let line of lines) {
            const orderedMatch = line.match(/^(\d+)\.\s+(.+)$/);
            const unorderedMatch = line.match(/^[-*]\s+(.+)$/);

            if (orderedMatch) {
                if (!inOrderedList) {
                    if (inUnorderedList) {
                        result.push('</ul>');
                        inUnorderedList = false;
                    }
                    result.push('<ol>');
                    inOrderedList = true;
                }
                result.push(`<li>${orderedMatch[2]}</li>`);
            } else if (unorderedMatch) {
                if (!inUnorderedList) {
                    if (inOrderedList) {
                        result.push('</ol>');
                        inOrderedList = false;
                    }
                    result.push('<ul>');
                    inUnorderedList = true;
                }
                result.push(`<li>${unorderedMatch[1]}</li>`);
            } else {
                if (inOrderedList) {
                    result.push('</ol>');
                    inOrderedList = false;
                }
                if (inUnorderedList) {
                    result.push('</ul>');
                    inUnorderedList = false;
                }
                if (line.trim()) {
                    result.push(line + '<br>');
                } else {
                    result.push('<br>');
                }
            }
        }

        if (inOrderedList) result.push('</ol>');
        if (inUnorderedList) result.push('</ul>');

        return result.join('');
    }

    function showTyping() {
        const typingDiv = document.createElement('div');
        typingDiv.className = 'chat-message assistant typing';
        typingDiv.id = 'typing-' + Date.now();
        typingDiv.innerHTML = '<i class="bi bi-three-dots"></i> Thinking...';
        chatMessages.appendChild(typingDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
        return typingDiv.id;
    }

    function removeTyping(id) {
        const typingDiv = document.getElementById(id);
        if (typingDiv) {
            typingDiv.remove();
        }
    }
});
