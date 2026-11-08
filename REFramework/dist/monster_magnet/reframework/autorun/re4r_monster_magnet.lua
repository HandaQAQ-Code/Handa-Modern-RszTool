local SETTINGS_PATH = "re4r_monster_companion_teleport.json"

local settings = {
    enabled = true,
    far_distance = 55.0,
    companion_distance = 28.0,
    scan_interval_frames = 20,
    cooldown_frames = 240,
    max_teleports_per_scan = 1,
    destination_offset = 1.25,
    y_offset = 0.0,
    fallback_to_player_if_no_companion = false,
    fallback_player_distance = 8.0,
    use_3d_distance = false,
    require_combat_ready = false,
    verify_after_set = true,
    verify_distance = 3.0,
    debug = false
}

local state = {
    frame = 0,
    known_enemies = {},
    cooldown_until = {},
    last_enemy_count = 0,
    last_near_count = 0,
    last_far_count = 0,
    last_teleports = 0,
    total_teleports = 0,
    last_set_method = "",
    last_set_error = "",
    status = "Ready",
    notification = "",
    notification_frames = 0
}

local character_manager = nil
local transform_type = nil
local character_controller_type = nil
local game_object_type = nil
local get_transform_method = nil

local unpack_args = table.unpack or unpack

local function safe_call(fn)
    local ok, value = pcall(fn)
    if ok then
        return value
    end
    return nil
end

local function call_method(obj, method_name, ...)
    if obj == nil then
        return nil
    end

    local args = { ... }
    return safe_call(function()
        return obj:call(method_name, unpack_args(args))
    end)
end

local function try_call_method(obj, method_name, ...)
    if obj == nil then
        return false, nil
    end

    local args = { ... }
    local ok, value = pcall(function()
        return obj:call(method_name, unpack_args(args))
    end)
    return ok, value
end

local function notify(message)
    state.status = message
    state.notification = message
    state.notification_frames = 150
end

local function copy_defaults(target, defaults)
    for key, value in pairs(defaults) do
        if target[key] == nil then
            target[key] = value
        end
    end
end

local function load_settings()
    local loaded = safe_call(function()
        return json.load_file(SETTINGS_PATH)
    end)

    if type(loaded) == "table" then
        copy_defaults(loaded, settings)
        settings = loaded
        notify("Loaded settings")
    end
end

local function save_settings()
    safe_call(function()
        json.dump_file(SETTINGS_PATH, settings)
    end)
end

local function init_types()
    transform_type = transform_type or safe_call(function()
        return sdk.typeof("via.Transform")
    end)
    character_controller_type = character_controller_type or safe_call(function()
        return sdk.typeof("via.physics.CharacterController")
    end)
    game_object_type = game_object_type or safe_call(function()
        return sdk.find_type_definition("via.GameObject")
    end)
    get_transform_method = get_transform_method or (game_object_type and safe_call(function()
        return game_object_type:get_method("get_Transform")
    end) or nil)
end

local function get_character_manager()
    character_manager = character_manager or safe_call(function()
        return sdk.get_managed_singleton("chainsaw.CharacterManager")
    end)
    return character_manager
end

local function object_key(obj)
    if obj == nil then
        return nil
    end
    return tostring(obj)
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

local function to_vector3f(position)
    if position == nil then
        return nil
    end
    return Vector3f.new(position.x, position.y, position.z)
end

local function to_vector4f(position)
    if position == nil then
        return nil
    end
    return safe_call(function()
        return Vector4f.new(position.x, position.y, position.z, 1.0)
    end)
end

local function distance_sq(a, b)
    if a == nil or b == nil then
        return nil
    end

    local dx = a.x - b.x
    local dz = a.z - b.z
    local value = (dx * dx) + (dz * dz)

    if settings.use_3d_distance then
        local dy = a.y - b.y
        value = value + (dy * dy)
    end

    return value
end

local function get_position(obj, depth)
    depth = depth or 0
    if obj == nil or depth > 4 then
        return nil
    end

    local direct_methods = {
        "get_Position",
        "get_WorldPosition",
        "get_Pos",
        "get_UniversalPosition"
    }

    for _, method_name in ipairs(direct_methods) do
        local position = read_vector3(call_method(obj, method_name))
        if position ~= nil then
            return position
        end
    end

    local native_position = read_vector3(safe_call(function()
        return obj:get_position()
    end))
    if native_position ~= nil then
        return native_position
    end

    local child_methods = {
        "get_Transform",
        "get_GameObject",
        "get_GameObjectRef",
        "get_BodyGameObject",
        "get_ValidBodyGameObject",
        "get_BodyGameObjectRef",
        "get_BodyGameObjectRoot",
        "get_BodyObject",
        "get_CharaGameObject",
        "get_CharacterGameObject",
        "get_ModelGameObject",
        "get_RootGameObject",
        "get_OwnerGameObject",
        "getOwnerGameObject",
        "get_Body"
    }

    for _, method_name in ipairs(child_methods) do
        local child = call_method(obj, method_name)
        if child ~= nil and child ~= obj then
            local position = get_position(child, depth + 1)
            if position ~= nil then
                return position
            end
        end
    end

    return nil
end

local function get_player_position()
    local manager = get_character_manager()
    if manager ~= nil then
        local player = safe_call(function()
                return manager:getPlayerContextRef()
            end) or call_method(manager, "getPlayerContextRef") or
            call_method(manager, "get_MasterPlayer") or
            call_method(manager, "get_MainPlayer")

        local position = get_position(player)
        if position ~= nil then
            return position
        end

        local player_list = call_method(manager, "get_PlayerAndPartnerContextList") or
            call_method(manager, "get_PlayerContextList")
        local count = tonumber(call_method(player_list, "get_Count") or 0) or 0
        for i = 0, count - 1, 1 do
            position = get_position(call_method(player_list, "get_Item", i))
            if position ~= nil then
                return position
            end
        end
    end

    return nil
end

local function get_game_object(obj)
    if obj == nil then
        return nil
    end

    local native_game_object = safe_call(function()
        return obj:get_game_object()
    end)
    if native_game_object ~= nil then
        return native_game_object
    end

    local methods = {
        "get_GameObject",
        "get_GameObjectRef",
        "get_BodyGameObject",
        "get_ValidBodyGameObject",
        "get_BodyGameObjectRef",
        "get_BodyGameObjectRoot",
        "get_BodyObject",
        "get_CharaGameObject",
        "get_CharacterGameObject",
        "get_ModelGameObject",
        "get_RootGameObject",
        "get_OwnerGameObject",
        "getOwnerGameObject",
        "get_Body"
    }

    for _, method_name in ipairs(methods) do
        local game_object = call_method(obj, method_name)
        if game_object ~= nil then
            return game_object
        end
    end

    return nil
end

local function get_transform(obj)
    if obj == nil then
        return nil
    end

    local native_transform = safe_call(function()
        return obj:get_transform()
    end)
    if native_transform ~= nil then
        return native_transform
    end

    local transform = call_method(obj, "get_Transform") or
        call_method(obj, "get_transform")
    if transform ~= nil then
        return transform
    end

    if transform_type ~= nil then
        transform = call_method(obj, "getComponent(System.Type)", transform_type)
        if transform ~= nil then
            return transform
        end
    end

    local game_object = get_game_object(obj)
    if game_object ~= nil and game_object ~= obj then
        native_transform = safe_call(function()
            return game_object:get_transform()
        end)
        if native_transform ~= nil then
            return native_transform
        end

        if get_transform_method ~= nil then
            transform = safe_call(function()
                return get_transform_method:call(game_object)
            end)
            if transform ~= nil then
                return transform
            end
        end

        if transform_type ~= nil then
            transform = call_method(game_object, "getComponent(System.Type)", transform_type)
            if transform ~= nil then
                return transform
            end
        end
    end

    return nil
end

local function get_character_controller(obj)
    if obj == nil or character_controller_type == nil then
        return nil
    end

    local controller = call_method(obj, "getComponent(System.Type)", character_controller_type)
    if controller ~= nil then
        return controller
    end

    local game_object = get_game_object(obj)
    if game_object ~= nil and game_object ~= obj then
        controller = call_method(game_object, "getComponent(System.Type)", character_controller_type)
        if controller ~= nil then
            return controller
        end
    end

    return nil
end

local function set_transform_position(transform, position)
    if transform == nil or position == nil then
        return false, "no transform"
    end

    local vector3 = to_vector3f(position)
    local vector4 = to_vector4f(position)

    local ok = false
    if vector4 ~= nil then
        ok = pcall(function()
            transform:set_position(vector4, true)
        end)
        if ok then
            return true, "native transform:set_position"
        end
    end

    local setters = {
        "set_Position",
        "set_WorldPosition",
        "set_UniversalPosition"
    }

    for _, method_name in ipairs(setters) do
        ok = try_call_method(transform, method_name, vector3)
        if ok then
            return true, "transform:" .. method_name .. "(Vector3f)"
        end

        if vector4 ~= nil then
            ok = try_call_method(transform, method_name, vector4)
            if ok then
                return true, "transform:" .. method_name .. "(Vector4f)"
            end
        end
    end

    return false, "transform setters failed"
end

local function verify_position(obj, position, transform)
    if not settings.verify_after_set then
        return true
    end

    local actual = get_position(transform) or get_position(obj)
    if actual == nil then
        return true
    end

    local limit = tonumber(settings.verify_distance) or 3.0
    local dist = distance_sq(actual, position)
    return dist ~= nil and dist <= (limit * limit)
end

local function set_position(obj, position)
    local vector3 = to_vector3f(position)
    local vector4 = to_vector4f(position)
    if vector3 == nil then
        return false, "invalid destination"
    end

    local controller = get_character_controller(obj)
    local transform = get_transform(obj)

    if transform ~= nil then
        if controller ~= nil then
            try_call_method(controller, "warp")
        end

        local ok, method = set_transform_position(transform, position)
        if ok then
            if controller ~= nil then
                try_call_method(controller, "warp")
            end

            if verify_position(obj, position, transform) then
                return true, method
            end
            state.last_set_error = method .. " did not move close enough"
        else
            state.last_set_error = method
        end
    else
        state.last_set_error = "no transform found"
    end

    local direct_setters = {
        "set_Position",
        "set_WorldPosition",
        "set_UniversalPosition"
    }

    for _, method_name in ipairs(direct_setters) do
        local ok = try_call_method(obj, method_name, vector3)
        if ok and verify_position(obj, position, transform) then
            return true, "context:" .. method_name .. "(Vector3f)"
        end

        if vector4 ~= nil then
            ok = try_call_method(obj, method_name, vector4)
            if ok and verify_position(obj, position, transform) then
                return true, "context:" .. method_name .. "(Vector4f)"
            end
        end
    end

    return false, state.last_set_error ~= "" and state.last_set_error or "all setters failed"
end

local function add_enemy(enemies, seen, enemy)
    if enemy == nil then
        return
    end

    local key = object_key(enemy)
    if key == nil or seen[key] then
        return
    end

    seen[key] = true
    table.insert(enemies, enemy)
end

local function add_enemy_collection(enemies, seen, collection)
    if collection == nil then
        return
    end

    local count = tonumber(call_method(collection, "get_Count") or call_method(collection, "get_size") or 0) or 0
    if count > 0 then
        for i = 0, count - 1, 1 do
            add_enemy(enemies, seen, call_method(collection, "get_Item", i) or call_method(collection, "get_element", i))
        end
        return
    end

    local elements = call_method(collection, "get_elements")
    if type(elements) == "table" then
        for _, enemy in ipairs(elements) do
            add_enemy(enemies, seen, enemy)
        end
    end
end

local function collect_enemies_from_managers(enemies, seen)
    local manager = get_character_manager()
    if manager ~= nil then
        local collections = {
            call_method(manager, "get_EnemyContextList"),
            call_method(manager, "get_EnemyContextRefList"),
            call_method(manager, "get_EnemyBaseContextList"),
            call_method(manager, "get_EnemyList")
        }

        for _, collection in ipairs(collections) do
            add_enemy_collection(enemies, seen, collection)
        end

        local enemy_manager = call_method(manager, "get_EnemyManager")
        if enemy_manager ~= nil then
            local manager_collections = {
                call_method(enemy_manager, "get_EnemyContextList"),
                call_method(enemy_manager, "get_EnemyContextRefList"),
                call_method(enemy_manager, "get_EnemyBaseContextList"),
                call_method(enemy_manager, "get_EnemyList")
            }

            for _, collection in ipairs(manager_collections) do
                add_enemy_collection(enemies, seen, collection)
            end
        end
    end

    local singleton_names = {
        "chainsaw.EnemyManager",
        "chainsaw.EnemyContextManager"
    }

    for _, singleton_name in ipairs(singleton_names) do
        local singleton = safe_call(function()
            return sdk.get_managed_singleton(singleton_name)
        end)
        if singleton ~= nil then
            add_enemy_collection(enemies, seen, call_method(singleton, "get_EnemyContextList"))
            add_enemy_collection(enemies, seen, call_method(singleton, "get_EnemyContextRefList"))
            add_enemy_collection(enemies, seen, call_method(singleton, "get_EnemyList"))
        end
    end
end

local function collect_enemies()
    local enemies = {}
    local seen = {}

    collect_enemies_from_managers(enemies, seen)

    for key, enemy in pairs(state.known_enemies) do
        if enemy ~= nil and state.cooldown_until[key] ~= -1 then
            add_enemy(enemies, seen, enemy)
        end
    end

    return enemies
end

local function remember_enemy(enemy)
    if enemy == nil then
        return
    end

    local key = object_key(enemy)
    if key ~= nil then
        state.known_enemies[key] = enemy
    end
end

local function is_enemy_alive(enemy)
    if enemy == nil then
        return false
    end

    local lively = call_method(enemy, "get_IsLively")
    if lively == false then
        return false
    end

    if settings.require_combat_ready then
        local combat_ready = call_method(enemy, "get_IsCombatReady")
        if combat_ready == false then
            return false
        end
    end

    local hp = call_method(enemy, "get_HitPoint")
    if hp ~= nil then
        local current = tonumber(call_method(hp, "get_CurrentHitPoint"))
        if current ~= nil and current <= 0 then
            return false
        end
    end

    return get_position(enemy) ~= nil
end

local function make_destination(player_position, companion_position, serial)
    local destination = {
        x = companion_position.x,
        y = companion_position.y + settings.y_offset,
        z = companion_position.z
    }

    local offset = tonumber(settings.destination_offset) or 0.0
    if offset <= 0.0 then
        return destination
    end

    local dx = companion_position.x - player_position.x
    local dz = companion_position.z - player_position.z
    local length = math.sqrt((dx * dx) + (dz * dz))

    if length < 0.001 then
        local angle = (serial or 1) * 2.399963
        dx = math.cos(angle)
        dz = math.sin(angle)
        length = 1.0
    end

    destination.x = destination.x + (dx / length) * offset
    destination.z = destination.z + (dz / length) * offset
    return destination
end

local function make_player_fallback_destination(player_position, far_position, serial)
    local distance = math.max(1.0, tonumber(settings.fallback_player_distance) or 8.0)
    local dx = far_position.x - player_position.x
    local dz = far_position.z - player_position.z
    local length = math.sqrt((dx * dx) + (dz * dz))

    if length < 0.001 then
        local angle = (serial or 1) * 2.399963
        dx = math.cos(angle)
        dz = math.sin(angle)
        length = 1.0
    end

    return {
        x = player_position.x + (dx / length) * distance,
        y = player_position.y + settings.y_offset,
        z = player_position.z + (dz / length) * distance
    }
end

local function choose_companion(candidates, far_key)
    local best = nil
    local best_distance_sq = nil

    for _, candidate in ipairs(candidates) do
        if candidate.key ~= far_key then
            if best == nil or candidate.distance_sq < best_distance_sq then
                best = candidate
                best_distance_sq = candidate.distance_sq
            end
        end
    end

    return best
end

local function update_teleport(force)
    state.frame = state.frame + 1
    state.last_teleports = 0

    if not settings.enabled then
        return
    end

    local interval = math.max(1, math.floor(tonumber(settings.scan_interval_frames) or 1))
    if not force and state.frame % interval ~= 0 then
        return
    end

    init_types()

    local player_position = get_player_position()
    if player_position == nil then
        state.last_enemy_count = 0
        state.last_near_count = 0
        state.last_far_count = 0
        state.status = "Waiting for player"
        return
    end

    local far_limit_sq = settings.far_distance * settings.far_distance
    local companion_limit_sq = settings.companion_distance * settings.companion_distance
    local enemies = collect_enemies()
    local near = {}
    local far = {}

    for _, enemy in ipairs(enemies) do
        remember_enemy(enemy)

        if is_enemy_alive(enemy) then
            local position = get_position(enemy)
            local dist_sq = distance_sq(player_position, position)
            if dist_sq ~= nil then
                local item = {
                    enemy = enemy,
                    key = object_key(enemy),
                    position = position,
                    distance_sq = dist_sq
                }

                if dist_sq <= companion_limit_sq then
                    table.insert(near, item)
                end
                if dist_sq > far_limit_sq then
                    table.insert(far, item)
                end
            end
        end
    end

    state.last_enemy_count = #enemies
    state.last_near_count = #near
    state.last_far_count = #far

    local use_player_fallback = #near == 0 and settings.fallback_to_player_if_no_companion
    if #near == 0 and not use_player_fallback then
        state.status = "No companion near player"
        return
    end

    local max_teleports = math.max(1, math.floor(tonumber(settings.max_teleports_per_scan) or 1))
    local cooldown = math.max(0, math.floor(tonumber(settings.cooldown_frames) or 0))

    for _, far_item in ipairs(far) do
        if state.last_teleports >= max_teleports then
            break
        end

        local key = far_item.key
        local cooldown_until = state.cooldown_until[key] or 0
        if state.frame >= cooldown_until then
            local companion = choose_companion(near, key)
            if companion ~= nil or use_player_fallback then
                local destination = companion ~= nil and
                    make_destination(player_position, companion.position, state.total_teleports + 1) or
                    make_player_fallback_destination(player_position, far_item.position, state.total_teleports + 1)
                local moved, method = set_position(far_item.enemy, destination)
                if moved then
                    state.last_set_method = method or "unknown"
                    state.last_set_error = ""
                    state.cooldown_until[key] = state.frame + cooldown
                    state.last_teleports = state.last_teleports + 1
                    state.total_teleports = state.total_teleports + 1
                    if companion ~= nil then
                        notify(string.format(
                            "Teleported enemy %.1fm -> companion %.1fm",
                            math.sqrt(far_item.distance_sq),
                            math.sqrt(companion.distance_sq)
                        ))
                    else
                        notify(string.format(
                            "Teleported enemy %.1fm -> player fallback",
                            math.sqrt(far_item.distance_sq)
                        ))
                    end
                else
                    state.last_set_method = method or ""
                    state.status = "Found enemy but could not set position: " .. tostring(method)
                end
            end
        end
    end

    if state.last_teleports == 0 then
        state.status = string.format("Enemies %d, near %d, far %d", #enemies, #near, #far)
    end
end

local function hook_enemy_type(type_name)
    local type_def = safe_call(function()
        return sdk.find_type_definition(type_name)
    end)
    if type_def == nil then
        return 0
    end

    local hooked = 0
    local names = {
        "update",
        "update()",
        "Update",
        "lateUpdate",
        "lateUpdate()",
        "onUpdate",
        "onUpdate()"
    }

    for _, method_name in ipairs(names) do
        local method = safe_call(function()
            return type_def:get_method(method_name)
        end)

        if method ~= nil then
            local ok = pcall(function()
                sdk.hook(method, function(args)
                    local enemy = safe_call(function()
                        return sdk.to_managed_object(args[2])
                    end) or safe_call(function()
                        return sdk.to_managed_object(args[1])
                    end)
                    remember_enemy(enemy)
                end, function(retval)
                    return retval
                end)
            end)

            if ok then
                hooked = hooked + 1
            end
        end
    end

    return hooked
end

local function install_enemy_hooks()
    local total = 0
    local type_names = {
        "chainsaw.EnemyContext",
        "chainsaw.EnemyBaseContext"
    }

    for _, type_name in ipairs(type_names) do
        total = total + hook_enemy_type(type_name)
    end

    if total > 0 then
        state.status = "Installed enemy update hooks: " .. tostring(total)
    end
end

local function draw_notification()
    if state.notification_frames <= 0 or state.notification == "" then
        return
    end

    draw.filled_rect(80, 80, 700, 42, 0xCC1F1F1F)
    draw.text(state.notification, 100, 92, 0xFFFFFFFF)
    state.notification_frames = state.notification_frames - 1
end

local function draw_settings_ui()
    local changed = false
    local item_changed = false

    item_changed, settings.enabled = imgui.checkbox("启用", settings.enabled)
    changed = changed or item_changed

    item_changed, settings.far_distance = imgui.drag_float("远距离距离定义", settings.far_distance, 1.0, 5.0, 300.0)
    changed = changed or item_changed

    item_changed, settings.companion_distance = imgui.drag_float("伙伴最大距离定义", settings.companion_distance,
        1.0, 1.0, 200.0)
    changed = changed or item_changed

    item_changed, settings.destination_offset = imgui.drag_float("目标偏移量", settings.destination_offset, 0.1,
        0.0, 10.0)
    changed = changed or item_changed

    item_changed, settings.y_offset = imgui.drag_float("Y 偏移", settings.y_offset, 0.05, -5.0, 5.0)
    changed = changed or item_changed

    item_changed, settings.fallback_to_player_if_no_companion = imgui.checkbox("没有伙伴时敌人传送到玩家",
        settings.fallback_to_player_if_no_companion)
    changed = changed or item_changed

    item_changed, settings.fallback_player_distance = imgui.drag_float("没有伙伴时敌人传送到玩家距离",
        settings.fallback_player_distance, 0.25, 1.0, 50.0)
    changed = changed or item_changed

    item_changed, settings.scan_interval_frames = imgui.drag_int("扫描间隔帧数", settings.scan_interval_frames, 1,
        1, 300)
    changed = changed or item_changed

    item_changed, settings.cooldown_frames = imgui.drag_int("冷却帧数", settings.cooldown_frames, 5, 0, 3600)
    changed = changed or item_changed

    item_changed, settings.max_teleports_per_scan = imgui.drag_int("最大传送数量",
        settings.max_teleports_per_scan, 1, 1, 20)
    changed = changed or item_changed

    item_changed, settings.use_3d_distance = imgui.checkbox("使用3D 距离", settings.use_3d_distance)
    changed = changed or item_changed

    item_changed, settings.require_combat_ready = imgui.checkbox("需要战斗准备", settings.require_combat_ready)
    changed = changed or item_changed

    item_changed, settings.verify_after_set = imgui.checkbox("设置后验证位置", settings.verify_after_set)
    changed = changed or item_changed

    item_changed, settings.verify_distance = imgui.drag_float("验证距离", settings.verify_distance, 0.1, 0.1, 20.0)
    changed = changed or item_changed

    item_changed, settings.debug = imgui.checkbox("调试", settings.debug)
    changed = changed or item_changed

    if changed then
        save_settings()
    end

    if imgui.button("保存设置") then
        save_settings()
        notify("设置已保存")
    end

    imgui.same_line()
    if imgui.button("重新加载设置") then
        load_settings()
    end

    if imgui.button("强制扫描") then
        update_teleport(true)
    end

    imgui.separator()
    imgui.text("状态: " .. tostring(state.status))
    imgui.text(string.format(
        "敌人数量: %d | 近敌人: %d | 远敌人数量: %d | 已传送数量: %d",
        state.last_enemy_count,
        state.last_near_count,
        state.last_far_count,
        state.total_teleports
    ))

    if settings.debug then
        local known = 0
        for _ in pairs(state.known_enemies) do
            known = known + 1
        end
        imgui.text("已知敌人数量: " .. tostring(known))
        imgui.text("当前帧数: " .. tostring(state.frame))
        imgui.text("最后设置方法: " .. tostring(state.last_set_method))
        imgui.text("最后设置错误: " .. tostring(state.last_set_error))
    end
end

load_settings()
install_enemy_hooks()

re.on_frame(function()
    update_teleport(false)
    draw_notification()
end)

re.on_draw_ui(function()
    if imgui.tree_node("RE4R 怪物磁铁 By HandaQAQ") then
        draw_settings_ui()
        imgui.tree_pop()
    end
end)
