-- Copy this file to: <RE4 game folder>/reframework/autorun/
-- Exported JSON is written through REFramework's json API, normally under reframework/data/.

local PROJECT_PATH = "re4r_mercenaries_spawn_point_manager.json"
local EXPORT_PATH = "re4r_mercenaries_spawn_points_export.json"
local FORMAT = "re4r.mercenaries_spawn_points"
local FORMAT_VERSION = 2

local data = {
    format = FORMAT,
    version = FORMAT_VERSION,
    activeGroup = "Default",
    groups = {
        {
            name = "Default",
            points = {}
        }
    }
}

local ui = {
    new_group_name = "Group 1",
    status = "Ready",
    window_open = true,
    notification = "",
    notification_frames = 0
}

local function safe_call(fn)
    local ok, value = pcall(fn)
    if ok then
        return value
    end
    return nil
end

local characterManager = safe_call(function()
    return sdk.get_managed_singleton("chainsaw.CharacterManager")
end)
local GameObject_typedef = safe_call(function()
    return sdk.find_type_definition("via.GameObject")
end)
local get_Transform = GameObject_typedef and safe_call(function()
    return GameObject_typedef:get_method("get_Transform")
end) or nil

local keyboard_singleton = safe_call(function()
    return sdk.get_native_singleton("via.hid.Keyboard")
end)
local keyboard_typedef = safe_call(function()
    return sdk.find_type_definition("via.hid.Keyboard")
end)
local keyboardkey_typedef = safe_call(function()
    return sdk.find_type_definition("via.hid.KeyboardKey")
end)
local hotkey_modifier_code = keyboardkey_typedef and safe_call(function()
    return keyboardkey_typedef:get_field("Menu"):get_data(nil)
end) or nil
local hotkey_capture_code = keyboardkey_typedef and safe_call(function()
    return keyboardkey_typedef:get_field("C"):get_data(nil)
end) or nil

local function notify(message)
    ui.notification = message
    ui.notification_frames = 180
    ui.status = message
end

local function round_coord(value)
    return math.floor((value * 1000.0) + 0.5) / 1000.0
end

local function read_component(value, names, index)
    if type(value) ~= "table" then
        return nil
    end

    for _, name in ipairs(names) do
        if value[name] ~= nil then
            return tonumber(value[name])
        end
    end
    return tonumber(value[index])
end

local function normalize_point(point)
    if type(point) ~= "table" then
        return nil
    end

    local transform = point.transform or point.position or point.pos or point.v0
    local x = read_component(transform, {"x", "X"}, 1) or tonumber(point.x or point[1])
    local y = read_component(transform, {"y", "Y"}, 2) or tonumber(point.y or point[2])
    local z = read_component(transform, {"z", "Z"}, 3) or tonumber(point.z or point[3])
    if x == nil or y == nil or z == nil then
        return nil
    end

    local rotation = point.rotation or point.rot or point.orientation or point.v1
    local rx = read_component(rotation, {"a", "x", "X"}, 1)
    local ry = read_component(rotation, {"b", "y", "Y"}, 2)
    local rz = read_component(rotation, {"c", "z", "Z"}, 3)
    local rw = read_component(rotation, {"d", "w", "W"}, 4)
    if rx ~= nil and ry ~= nil and rz ~= nil and rw ~= nil then
        return {
            transform = {round_coord(x), round_coord(y), round_coord(z)},
            rotation = {round_coord(rx), round_coord(ry), round_coord(rz), round_coord(rw)}
        }
    end

    -- Keep old x/y/z/b points readable and exportable.
    local b = point.b
    if b == nil then
        b = point[4]
    end
    if b == nil then
        b = 1
    end
    if b == true then
        b = 1
    elseif b == false then
        b = 0
    else
        b = tonumber(b) == 0 and 0 or 1
    end

    return {
        x = round_coord(x),
        y = round_coord(y),
        z = round_coord(z),
        b = b
    }
end

local function append_normalized_group(name, group)
    if type(group) ~= "table" then
        return
    end

    local points_source = group.points or group
    local normalized = {
        name = tostring(group.name or name),
        points = {}
    }
    local used_indexes = {}

    if type(points_source) == "table" then
        for i, point in ipairs(points_source) do
            used_indexes[i] = true
            local normalized_point = normalize_point(point)
            if normalized_point ~= nil then
                table.insert(normalized.points, normalized_point)
            end
        end

        for key, point in pairs(points_source) do
            if type(key) == "number" and not used_indexes[key] then
                local normalized_point = normalize_point(point)
                if normalized_point ~= nil then
                    table.insert(normalized.points, normalized_point)
                end
            end
        end
    end

    table.insert(data.groups, normalized)
end

local function normalize_data(source)
    if type(source) ~= "table" then
        return
    end

    data.format = source.format or FORMAT
    data.version = tonumber(source.version) or FORMAT_VERSION
    data.activeGroup = source.activeGroup or source.active_group or source.currentGroup or "Default"
    data.groups = {}

    if type(source.groups) == "table" then
        local used_indexes = {}
        for i, group in ipairs(source.groups) do
            used_indexes[i] = true
            append_normalized_group("Group " .. i, group)
        end

        for key, group in pairs(source.groups) do
            if not used_indexes[key] then
                append_normalized_group(key, group)
            end
        end
    end

    if #data.groups == 0 then
        table.insert(data.groups, {
            name = "Default",
            points = {}
        })
        data.activeGroup = "Default"
    end
end

local function load_project()
    local loaded = safe_call(function()
        return json.load_file(PROJECT_PATH)
    end)
    if loaded ~= nil then
        normalize_data(loaded)
        ui.status = "Loaded " .. PROJECT_PATH
    end
end

local function save_project()
    data.format = FORMAT
    data.version = FORMAT_VERSION
    safe_call(function()
        json.dump_file(PROJECT_PATH, data)
    end)
end

local function export_json()
    data.format = FORMAT
    data.version = FORMAT_VERSION
    data.exportedAt = os.date("!%Y-%m-%dT%H:%M:%SZ")
    json.dump_file(EXPORT_PATH, data)
    ui.status = "Exported " .. EXPORT_PATH
end

local function get_group_index(name)
    for i, group in ipairs(data.groups) do
        if group.name == name then
            return i
        end
    end
    return nil
end

local function get_active_group()
    local index = get_group_index(data.activeGroup)
    if index == nil then
        index = 1
        data.activeGroup = data.groups[1].name
    end
    return data.groups[index], index
end

local function create_group(name)
    name = tostring(name or ""):gsub("^%s+", ""):gsub("%s+$", "")
    if name == "" then
        ui.status = "Group name is empty"
        return
    end
    if get_group_index(name) ~= nil then
        ui.status = "Group already exists: " .. name
        return
    end

    table.insert(data.groups, {
        name = name,
        points = {}
    })
    data.activeGroup = name
    ui.new_group_name = "Group " .. tostring(#data.groups + 1)
    ui.status = "Created group: " .. name
    save_project()
end

local function input_text(label, value)
    local changed, new_value = imgui.input_text(label, value)
    if type(changed) == "string" and new_value == nil then
        return changed ~= value, changed
    end
    if new_value == nil then
        new_value = value
    end
    return changed, new_value
end

local function call_method(obj, method_name, ...)
    if obj == nil then
        return nil
    end
    local args = {...}
    local unpack_args = table.unpack or unpack
    return safe_call(function()
        return obj:call(method_name, unpack_args(args))
    end)
end

local function read_vector3(value)
    if value == nil then
        return nil
    end

    local x = value.x or value.X
    local y = value.y or value.Y
    local z = value.z or value.Z

    if x == nil then
        x = safe_call(function() return value:get_field("x") end) or
            safe_call(function() return value:get_field("X") end)
    end
    if y == nil then
        y = safe_call(function() return value:get_field("y") end) or
            safe_call(function() return value:get_field("Y") end)
    end
    if z == nil then
        z = safe_call(function() return value:get_field("z") end) or
            safe_call(function() return value:get_field("Z") end)
    end

    if x ~= nil and y ~= nil and z ~= nil then
        return {
            x = tonumber(x),
            y = tonumber(y),
            z = tonumber(z)
        }
    end

    return nil
end

local function read_vector4(value)
    if value == nil then
        return nil
    end

    local x = value.x or value.X
    local y = value.y or value.Y
    local z = value.z or value.Z
    local w = value.w or value.W

    if x == nil then
        x = safe_call(function() return value:get_field("x") end) or
            safe_call(function() return value:get_field("X") end)
    end
    if y == nil then
        y = safe_call(function() return value:get_field("y") end) or
            safe_call(function() return value:get_field("Y") end)
    end
    if z == nil then
        z = safe_call(function() return value:get_field("z") end) or
            safe_call(function() return value:get_field("Z") end)
    end
    if w == nil then
        w = safe_call(function() return value:get_field("w") end) or
            safe_call(function() return value:get_field("W") end)
    end

    if x ~= nil and y ~= nil and z ~= nil and w ~= nil then
        return {
            x = tonumber(x),
            y = tonumber(y),
            z = tonumber(z),
            w = tonumber(w)
        }
    end

    return nil
end

local function try_position_methods(obj, depth)
    depth = depth or 0
    if depth > 4 then
        return nil
    end

    local position_methods = {
        "get_WorldPosition",
        "get_Position",
        "get_Pos",
        "get_UniversalPosition"
    }

    for _, method_name in ipairs(position_methods) do
        local position = read_vector3(call_method(obj, method_name))
        if position ~= nil then
            return position
        end
    end

    local transform_methods = {
        "get_Transform",
        "get_transform",
        "get_GameObject",
        "get_GameObjectRef"
    }

    for _, method_name in ipairs(transform_methods) do
        local child = call_method(obj, method_name)
        if child ~= nil and child ~= obj then
            local position = try_position_methods(child, depth + 1)
            if position ~= nil then
                return position
            end
        end
    end

    return nil
end

local function try_rotation_methods(obj, depth)
    depth = depth or 0
    if depth > 4 then
        return nil
    end

    local rotation_methods = {
        "get_WorldRotation",
        "get_Rotation",
        "get_Rot",
        "get_Orientation"
    }

    for _, method_name in ipairs(rotation_methods) do
        local rotation = read_vector4(call_method(obj, method_name))
        if rotation ~= nil then
            return rotation
        end
    end

    local transform_methods = {
        "get_Transform",
        "get_transform",
        "get_GameObject",
        "get_GameObjectRef"
    }

    for _, method_name in ipairs(transform_methods) do
        local child = call_method(obj, method_name)
        if child ~= nil and child ~= obj then
            local rotation = try_rotation_methods(child, depth + 1)
            if rotation ~= nil then
                return rotation
            end
        end
    end

    return nil
end

local function get_player_candidates_from_manager(manager)
    local candidates = {}
    if manager == nil then
        return candidates
    end

    local methods = {
        "get_MainPlayer",
        "get_MasterPlayer",
        "get_CurrentPlayer",
        "get_Player",
        "get_PlayerContext",
        "get_CurrentPlayerContext",
        "findMasterPlayer",
        "findPlayer",
        "get_PlayerObj"
    }

    for _, method_name in ipairs(methods) do
        local candidate = call_method(manager, method_name)
        if candidate ~= nil then
            table.insert(candidates, candidate)
        end
    end

    return candidates
end

local function get_player_transform()
    characterManager = characterManager or safe_call(function()
        return sdk.get_managed_singleton("chainsaw.CharacterManager")
    end)
    if characterManager ~= nil and get_Transform ~= nil then
        local player = safe_call(function()
            return characterManager:getPlayerContextRef()
        end)
        if player ~= nil then
            local body = safe_call(function()
                return player:get_BodyGameObject()
            end)
            local transform = body and safe_call(function()
                return get_Transform:call(body)
            end) or nil
            local pos = transform and safe_call(function()
                return transform:get_Position()
            end) or nil
            local rot = transform and safe_call(function()
                return transform:get_Rotation()
            end) or nil
            local position = read_vector3(pos)
            local rotation = read_vector4(rot)
            if position ~= nil and rotation ~= nil then
                return position, rotation
            end
        end
    end

    local manager_names = {
        "chainsaw.PlayerManager",
        "chainsaw.CharacterManager"
    }

    for _, type_name in ipairs(manager_names) do
        local manager = safe_call(function()
            return sdk.get_managed_singleton(type_name)
        end)

        for _, candidate in ipairs(get_player_candidates_from_manager(manager)) do
            local position = try_position_methods(candidate)
            local rotation = try_rotation_methods(candidate)
            if position ~= nil and rotation ~= nil then
                return position, rotation
            end
        end
    end

    return nil
end

local function capture_player_position(source)
    local group = get_active_group()
    local position, rotation = get_player_transform()
    if position == nil or rotation == nil then
        notify("Could not find player position or rotation")
        return
    end

    local point = {
        transform = {
            round_coord(position.x),
            round_coord(position.y),
            round_coord(position.z)
        },
        rotation = {
            round_coord(rotation.x),
            round_coord(rotation.y),
            round_coord(rotation.z),
            round_coord(rotation.w)
        }
    }

    table.insert(group.points, point)
    local prefix = source == "hotkey" and "Hotkey captured" or "Captured"
    notify(string.format(
        "%s %s: (%.3f, %.3f, %.3f, 0), (%.3f, %.3f, %.3f, %.3f)",
        prefix,
        group.name,
        point.transform[1],
        point.transform[2],
        point.transform[3],
        point.rotation[1],
        point.rotation[2],
        point.rotation[3],
        point.rotation[4]))
    save_project()
end

re.on_application_entry("UpdateHID", function()
    if keyboard_singleton == nil or keyboard_typedef == nil or hotkey_modifier_code == nil or hotkey_capture_code == nil then
        return
    end

    local kb = safe_call(function()
        return sdk.call_native_func(keyboard_singleton, keyboard_typedef, "get_Device")
    end)
    if kb ~= nil and kb:isDown(hotkey_modifier_code) and kb:isRelease(hotkey_capture_code) then
        capture_player_position("hotkey")
    end
end)

local function draw_group_list()
    imgui.text("Groups: " .. tostring(#data.groups))

    for i, group in ipairs(data.groups) do
        local selected = data.activeGroup == group.name
        local marker = selected and "> " or "  "
        local label = marker .. group.name .. " (" .. tostring(#group.points) .. ")##group_" .. tostring(i)
        if imgui.button(label) then
            data.activeGroup = group.name
            ui.status = "Selected group: " .. group.name
            save_project()
        end
    end

    local changed, value = input_text("New group name", ui.new_group_name)
    if changed then
        ui.new_group_name = value
    end
    if imgui.button("Create New Group") then
        create_group(ui.new_group_name)
    end
end

local function draw_active_group()
    local group, group_index = get_active_group()

    imgui.text("Active: " .. group.name)
    imgui.text("Points: " .. tostring(#group.points))
    imgui.text("Hotkey: Alt+C capture current player position and rotation")

    if imgui.button("Capture Player Position") then
        capture_player_position()
    end

    imgui.same_line()
    if imgui.button("Delete Last Point") then
        if #group.points > 0 then
            table.remove(group.points)
            ui.status = "Deleted last point from " .. group.name
            save_project()
        else
            ui.status = "Group has no points"
        end
    end

    if imgui.button("Clear Current Group") then
        group.points = {}
        ui.status = "Cleared group: " .. group.name
        save_project()
    end

    imgui.same_line()
    if imgui.button("Delete Current Group") then
        if #data.groups > 1 then
            table.remove(data.groups, group_index)
            data.activeGroup = data.groups[1].name
            ui.status = "Deleted group"
            save_project()
        else
            ui.status = "Cannot delete the last group"
        end
    end

    if imgui.button("Export JSON") then
        export_json()
    end

    imgui.same_line()
    if imgui.button("Reload JSON") then
        load_project()
    end

    if imgui.tree_node("Points##active_points") then
        for i, point in ipairs(group.points) do
            if point.transform ~= nil and point.rotation ~= nil then
                imgui.text(string.format(
                    "%03d: T(%.3f, %.3f, %.3f, 0) R(%.3f, %.3f, %.3f, %.3f)",
                    i,
                    point.transform[1],
                    point.transform[2],
                    point.transform[3],
                    point.rotation[1],
                    point.rotation[2],
                    point.rotation[3],
                    point.rotation[4]))
            else
                imgui.text(string.format("%03d: (%.3f, %.3f, %.3f, %d)", i, point.x, point.y, point.z, point.b))
            end
        end
        imgui.tree_pop()
    end
end

local function draw_manager_window()
    draw_group_list()
    imgui.separator()
    draw_active_group()
    imgui.separator()
    imgui.text("Project: " .. PROJECT_PATH)
    imgui.text("Export: " .. EXPORT_PATH)
    imgui.text("Status: " .. ui.status)
end

local function draw_notification()
    if ui.notification_frames <= 0 or ui.notification == "" then
        return
    end

    draw.filled_rect(80, 80, 760, 48, 0xCC1F1F1F)
    draw.text(ui.notification, 100, 94, 0xFFFFFFFF)
    ui.notification_frames = ui.notification_frames - 1
end

load_project()

re.on_frame(function()
    draw_notification()

    if not ui.window_open then
        return
    end

    local still_open = imgui.begin_window("RE4R Mercenaries Spawn Point Manager", true, 0)
    if still_open then
        draw_manager_window()
    end
    imgui.end_window()

    if not still_open then
        ui.window_open = false
    end
end)

re.on_draw_ui(function()
    if imgui.button("Open RE4R Mercenaries Spawn Point Manager") then
        ui.window_open = true
    end
end)
