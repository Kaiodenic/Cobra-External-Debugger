local global = _G
local table = global.table
local require = require
local CobraExternalDebuggerLuaDatabase = module(...)

CobraExternalDebuggerLuaDatabase.AddContentToCall = function(_tContentToCall)
    table.insert(_tContentToCall, require("Database.CobraExternalDebugger"))
end
