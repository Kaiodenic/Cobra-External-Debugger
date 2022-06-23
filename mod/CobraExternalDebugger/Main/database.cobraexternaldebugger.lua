local global = _G
local api = api
local type = global.type
local table = global.table
local pairs = global.pairs
local ipairs = global.ipairs
local string = global.string
local tostring = global.tostring
local CobraExternalDebugger = module(...)

CobraExternalDebugger.oldTrace = api.debug.Trace
CobraExternalDebugger.oldWarning = api.debug.Warning
CobraExternalDebugger.oldError = api.debug.Error
CobraExternalDebugger.oldQuit = api.game.Quit

api.cobradebugger = {}

CobraExternalDebugger.SendMessage = function(sData)
	local sURL = "http://localhost:8080/"
	local sParams = "?request=addmessage"
	local tHeaders = {"User-Agent: cobra-external-debugger", "Content-Type: application/json"}
	local nErr,tResponse = api.http.Post(sURL .. sParams, tHeaders, 200, sData)
	
	return tResponse
end

CobraExternalDebugger.SendMessageTask = function()
	while (api.cobradebugger.bSendMessages) do
		local nMessageCount = #api.cobradebugger.tMessageOutputQueue
		
		if nMessageCount > 0 then
			local tMessages = {}
			
			for i = 1,nMessageCount do
				local tMessage = table.remove(api.cobradebugger.tMessageOutputQueue, 1)
				table.insert(tMessages, { Type = tMessage.Type, Body = tMessage.Message })
			end
			
			local bSuccess, sMessageOut = false, ""
			
			if #tMessages > 0 then
				bSuccess, sMessageOut = api.json.Stringify(tMessages)
			end
			
			if bSuccess then
				CobraExternalDebugger.SendMessage(sMessageOut)
			end
		end
		
		api.task.YieldFrame()
	end
end

CobraExternalDebugger.ReceiveMessage = function()
	local sURL = "http://localhost:8080/"
	local sHeaders = "?request=getcommands"
	local nErr,tResponse = api.http.Get(sURL .. sHeaders, {"User-Agent: cobra-external-debugger"}, 200)
	
	if tResponse ~= nil then
		local bSuccess, tBody = ((api.json).Parse)(tResponse.body)
		
		if bSuccess and tBody ~= nil and type(tBody) == "table" then
			return tBody.Commands
		end
	end
	
	return nil
end

CobraExternalDebugger.ReceiveMessageTask = function()
	local nLastTickTime = api.time.GetTotalTimeUnscaled()
	local nTicKTime = 0.5
	local tMessageQueue = {}
	
	while (api.cobradebugger.bReceiveMessages) do
		local nCurrentTime = api.time.GetTotalTimeUnscaled()
		local nTimeDiff = nCurrentTime - nLastTickTime
		
		if nTimeDiff >= nTicKTime then
			nLastTickTime = nCurrentTime
			
			local tMessages = CobraExternalDebugger.ReceiveMessage()
			
			if tMessages ~= nil then
				for k,v in pairs(tMessages) do
					if v ~= nil then
						table.insert(tMessageQueue, v)
					end
				end
			end
		end
		
		if #tMessageQueue > 0 then
			local sMessage = table.remove(tMessageQueue, 1)
			api.debug.RunShellCommand(sMessage)
		end
		
		api.task.YieldFrame()
	end
end

CobraExternalDebugger.Setup = function()
	api.cobradebugger.tMessageOutputQueue = {}
	api.cobradebugger.tMessageInputQueue = {}
	api.cobradebugger.bSendMessages = false
	api.cobradebugger.bReceiveMessages = false
	api.cobradebugger.SendMessageTask = nil
	api.cobradebugger.ReceiveMessageTask = nil
	
	api.cobradebugger.StopMessageOutput = function()
		if api.cobradebugger.SendMessageTask ~= nil then
			api.cobradebugger.bSendMessages = false
			api.task.Cancel(api.cobradebugger.SendMessageTask)
			api.task.Join(api.cobradebugger.SendMessageTask)
			
			api.cobradebugger.bReceiveMessages = false
			api.task.Cancel(api.cobradebugger.ReceiveMessageTask)
			api.task.Join(api.cobradebugger.ReceiveMessageTask)
			
			api.cobradebugger.tMessageOutputQueue = {}
			api.cobradebugger.tMessageInputQueue = {}
		end
	end
	
	api.cobradebugger.StartMessageOutput = function()
		api.cobradebugger.StopMessageOutput()
		
		api.cobradebugger.bSendMessages = true
		api.cobradebugger.bReceiveMessages = true
		
		api.cobradebugger.SendMessageTask = api.task.Spawn(CobraExternalDebugger.SendMessageTask)
		api.cobradebugger.ReceiveMessageTask = api.task.Spawn(CobraExternalDebugger.ReceiveMessageTask)
	end
	
	api.cobradebugger.AddMessageToQueue = function(sType, sMessage)
		table.insert(api.cobradebugger.tMessageOutputQueue, { Type = sType, Message = sMessage })
	end
	
	api.game.Quit = function(bIsQuitting)
		api.cobradebugger.StopMessageOutput()
		CobraExternalDebugger.oldQuit(bIsQuitting)
	end
	
	api.debug.Trace = function(text)
		CobraExternalDebugger.oldTrace(text)
		api.cobradebugger.AddMessageToQueue("Standard", text)
	end
	
	api.debug.Warning = function(text)
		CobraExternalDebugger.oldWarning(text)
		api.cobradebugger.AddMessageToQueue("Warning", text)
	end
	
	api.debug.Error = function(text)
		CobraExternalDebugger.oldError(text)
		api.cobradebugger.AddMessageToQueue("Error", text)
	end
	
	api.cobradebugger.StartMessageOutput()
	
end
