-- Copy this file to: <RE4 game folder>/reframework/autorun/
-- F6 opens the upgrade shop. Kills are used as currency.

local SAVE_PATH = "re4r_upgrade_shop_save.json"
local HOTKEY_NAME = "F6"

local data = {
    version = 1,
    currency = 0,
    total_kills = 0,
    upgrades = {
        speed = 0,
        melee = 0,
        damage = 0,
        reduction = 0
    }
}

local shop = {
    open = false,
    pause_on_open = true,
    hotkey_name = HOTKEY_NAME
}

local state = {
    frame = 0,
    enemy_records = {},
    ignored_bonus_until = {},
    player_last_hp = nil,
    last_enemy_count = 0,
    last_kill_message = "",
    status = "Ready",
    pause_status = "Not paused",
    pause_applied = false,
    pause_method = "",
    speed_status = "Not applied",
    damage_status = "Ready",
    reduction_status = "Ready",
    notification = "",
    notification_frames = 0
}

local upgrade_defs = {
    {
        id = "speed",
        title = "Player Speed",
        max_level = 5,
        base_cost = 2,
        cost_step = 2,
        value_text = function(level)
            return string.format("+%d%% move speed", level * 8)
        end
    },
    {
        id = "melee",
        title = "Melee Range",
        max_level = 5,
        base_cost = 2,
        cost_step = 2,
        value_text = function(level)
            if level <= 0 then
                return "No melee splash"
            end
            return string.format("%.1fm assist radius", get_melee_radius(level))
        end
    },
    {
        id = "damage",
        title = "Damage Multiplier",
        max_level = 5,
        base_cost = 3,
        cost_step = 3,
        value_text = function(level)
            return string.format("x%.2f outgoing damage", get_damage_multiplier(level))
        end
    },
    {
        id = "reduction",
        title = "Damage Reduction",
        max_level = 5,
        base_cost = 3,
        cost_step = 3,
        value_text = function(level)
            return string.format("%d%% incoming damage reduction", math.floor(get_damage_reduction(level) * 100.0 + 0.5))
        end
    }
}

local character_manager = nil
local keyboard_singleton = nil
local keyboard_type = nil
local keyboard_key_type = nil
local hotkey_code = nil
local game_object_type = nil
local get_transform_method = nil
local transform_type = nil

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

    local args = {...}
    return safe_call(function()
        return obj:call(method_name, unpack_args(args))
    end)
end

local function try_call_method(obj, method_name, ...)
    if obj == nil then
        return false, nil
    end

    local args = {...}
    local ok, value = pcall(function()
        return obj:call(method_name, unpack_args(args))
    end)
    return ok, value
end

local function notify(message)
    state.status = message
    state.notification = message
    state.notification_frames = 180
end

function get_damage_multiplier(level)
    return 1.0 + ((tonumber(level) or 0) * 0.18)
end

function get_damage_reduction(level)
    return math.min(0.45, (tonumber(level) or 0) * 0.08)
end

function get_melee_radius(level)
    if level <= 0 then
        return 0.0
    end
    return 2.0 + (level * 0.65)
end

local function get_upgrade_level(id)
    data.upgrades = data.upgrades or {}
    return tonumber(data.upgrades[id]) or 0
end

local function set_upgrade_level(id, level)
    data.upgrades = data.upgrades or {}
    data.upgrades[id] = level
end

local function copy_defaults(target, defaults)
    for key, value in pairs(defaults) do
        if target[key] == nil then
            if type(value) == "table" then
                target[key] = {}
                copy_defaults(target[key], value)
            else
                target[key] = value
            end
        elseif type(value) == "table" and type(target[key]) == "table" then
            copy_defaults(target[key], value)
        end
    end
end

local function load_data()
    local loaded = safe_call(function()
        return json.load_file(SAVE_PATH)
    end)

    if type(loaded) == "table" then
        copy_defaults(loaded, data)
        data = loaded
        state.status = "Loaded save"
    end
end

local function save_data()
    data.version = 1
    safe_call(function()
        json.dump_file(SAVE_PATH, data)
    end)
end

local function get_cost(def)
    local level = get_upgrade_level(def.id)
    return def.base_cost + (level * def.cost_step)
end

local function can_buy(def)
    local level = get_upgrade_level(def.id)
    return level < def.max_level and data.currency >= get_cost(def)
end

local function buy_upgrade(def)
    local level = get_upgrade_level(def.id)
    if level >= def.max_level then
        notify(def.title .. " is already maxed")
        return
    end

    local cost = get_cost(def)
    if data.currency < cost then
        notify("Not enough kills")
        return
    end

    data.currency = data.currency - cost
    set_upgrade_level(def.id, level + 1)
    save_data()
    notify("Upgraded " .. def.title .. " to Lv." .. tostring(level + 1))
end

local function init_types()
    character_manager = character_manager or safe_call(function()
        return sdk.get_managed_singleton("chainsaw.CharacterManager")
    end)
    transform_type = transform_type or safe_call(function()
        return sdk.typeof("via.Transform")
    end)
    game_object_type = game_object_type or safe_call(function()
        return sdk.find_type_definition("via.GameObject")
    end)
    get_transform_method = get_transform_method or (game_object_type and safe_call(function()
        return game_object_type:get_method("get_Transform")
    end) or nil)

    keyboard_singleton = keyboard_singleton or safe_call(function()
        return sdk.get_native_singleton("via.hid.Keyboard")
    end)
    keyboard_type = keyboard_type or safe_call(function()
        return sdk.find_type_definition("via.hid.Keyboard")
    end)
    keyboard_key_type = keyboard_key_type or safe_call(function()
        return sdk.find_type_definition("via.hid.KeyboardKey")
    end)
    hotkey_code = hotkey_code or (keyboard_key_type and safe_call(function()
        return keyboard_key_type:get_field(shop.hotkey_name):get_data(nil)
    end) or nil)
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

local function distance_sq(a, b)
    if a == nil or b == nil then
        return nil
    end

    local dx = a.x - b.x
    local dy = a.y - b.y
    local dz = a.z - b.z
    return (dx * dx) + (dy * dy) + (dz * dz)
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

    local transform = call_method(obj, "get_Transform") or call_method(obj, "get_transform")
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

local function get_position(obj, depth)
    depth = depth or 0
    if obj == nil or depth > 4 then
        return nil
    end

    local methods = {
        "get_Position",
        "get_WorldPosition",
        "get_Pos",
        "get_UniversalPosition"
    }

    for _, method_name in ipairs(methods) do
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
        "get_Body",
        "get_CharaGameObject"
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

local function get_character_manager()
    character_manager = character_manager or safe_call(function()
        return sdk.get_managed_singleton("chainsaw.CharacterManager")
    end)
    return character_manager
end

local function get_player_context()
    local manager = get_character_manager()
    if manager == nil then
        return nil
    end

    return safe_call(function()
        return manager:getPlayerContextRef()
    end) or call_method(manager, "getPlayerContextRef") or
        call_method(manager, "get_MasterPlayer") or
        call_method(manager, "get_MainPlayer")
end

local function get_hitpoint(ctx)
    return call_method(ctx, "get_HitPoint")
end

local function get_hp_values(ctx)
    local hp = get_hitpoint(ctx)
    if hp == nil then
        return nil
    end

    local current = tonumber(call_method(hp, "get_CurrentHitPoint"))
    local max_hp = tonumber(call_method(hp, "get_DefaultHitPoint") or call_method(hp, "get_MaxHitPoint"))
    if current == nil then
        return nil
    end

    return {
        hp = hp,
        current = current,
        max = max_hp or current
    }
end

local function set_current_hp(hp, value)
    if hp == nil or value == nil then
        return false
    end

    value = math.max(0.0, tonumber(value) or 0.0)
    local methods = {
        "set_CurrentHitPoint",
        "set_CurrentHitPoint(System.Single)",
        "set_CurrentHitPoint(System.Int32)",
        "set_HitPoint",
        "set_HitPoint(System.Single)"
    }

    for _, method_name in ipairs(methods) do
        local ok = try_call_method(hp, method_name, value)
        if ok then
            return true
        end
    end

    local fields = {
        "_CurrentHitPoint",
        "<CurrentHitPoint>k__BackingField",
        "_HitPoint",
        "<HitPoint>k__BackingField"
    }

    for _, field_name in ipairs(fields) do
        local ok = pcall(function()
            hp:set_field(field_name, value)
        end)
        if ok then
            return true
        end
    end

    return false
end

local function recover_hp(hp, amount)
    if hp == nil or amount <= 0.0 then
        return false
    end

    local ok = try_call_method(hp, "recovery", amount)
    if ok then
        return true
    end

    ok = try_call_method(hp, "recovery(System.Single)", amount)
    if ok then
        return true
    end

    return false
end

local function add_enemy(enemies, seen, enemy)
    if enemy == nil then
        return
    end

    local key = tostring(enemy)
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
    if count <= 0 then
        return
    end

    for i = 0, count - 1, 1 do
        add_enemy(enemies, seen, call_method(collection, "get_Item", i) or call_method(collection, "get_element", i))
    end
end

local function collect_enemies()
    local enemies = {}
    local seen = {}
    local manager = get_character_manager()

    if manager ~= nil then
        add_enemy_collection(enemies, seen, call_method(manager, "get_EnemyContextList"))
        add_enemy_collection(enemies, seen, call_method(manager, "get_EnemyContextRefList"))
        add_enemy_collection(enemies, seen, call_method(manager, "get_EnemyBaseContextList"))
        add_enemy_collection(enemies, seen, call_method(manager, "get_EnemyList"))

        local enemy_manager = call_method(manager, "get_EnemyManager")
        if enemy_manager ~= nil then
            add_enemy_collection(enemies, seen, call_method(enemy_manager, "get_EnemyContextList"))
            add_enemy_collection(enemies, seen, call_method(enemy_manager, "get_EnemyContextRefList"))
            add_enemy_collection(enemies, seen, call_method(enemy_manager, "get_EnemyList"))
        end
    end

    state.last_enemy_count = #enemies
    return enemies
end

local function is_enemy_dead(enemy, hp_values)
    if hp_values ~= nil and hp_values.current <= 0.0 then
        return true
    end

    local lively = call_method(enemy, "get_IsLively")
    if lively == false then
        return true
    end

    return false
end

local function award_kill(key)
    local record = state.enemy_records[key]
    if record == nil or record.counted then
        return
    end

    record.counted = true
    data.currency = data.currency + 1
    data.total_kills = data.total_kills + 1
    state.last_kill_message = "Kill +" .. tostring(1)
    save_data()
    notify("Kill confirmed. Currency +" .. tostring(1))
end

local function damage_hitpoint(ctx, amount)
    local hp_values = get_hp_values(ctx)
    if hp_values == nil or amount <= 0.0 then
        return false
    end

    local target = math.max(0.0, hp_values.current - amount)
    return set_current_hp(hp_values.hp, target)
end

local function apply_melee_splash(source_enemy, source_key, source_position, damage_delta)
    local level = get_upgrade_level("melee")
    if level <= 0 or damage_delta <= 0.0 then
        return
    end

    local player_position = get_position(get_player_context())
    if player_position == nil or source_position == nil then
        return
    end

    local radius = get_melee_radius(level)
    local player_dist = distance_sq(player_position, source_position)
    if player_dist == nil or player_dist > ((radius + 1.25) * (radius + 1.25)) then
        return
    end

    local splash_damage = damage_delta * (0.16 * level)
    local splash_radius_sq = radius * radius

    for _, enemy in ipairs(collect_enemies()) do
        local key = tostring(enemy)
        if key ~= source_key and enemy ~= source_enemy then
            local pos = get_position(enemy)
            local dist = distance_sq(source_position, pos)
            if dist ~= nil and dist <= splash_radius_sq then
                if damage_hitpoint(enemy, splash_damage) then
                    state.ignored_bonus_until[key] = state.frame + 2
                end
            end
        end
    end
end

local function process_enemy(enemy)
    if enemy == nil then
        return
    end

    local key = tostring(enemy)
    local hp_values = get_hp_values(enemy)
    local dead = is_enemy_dead(enemy, hp_values)
    local record = state.enemy_records[key]

    if record == nil then
        state.enemy_records[key] = {
            hp = hp_values and hp_values.current or nil,
            was_alive = not dead,
            counted = dead
        }
        return
    end

    if dead then
        if record.was_alive then
            award_kill(key)
        end
        record.hp = hp_values and hp_values.current or 0.0
        record.was_alive = false
        return
    end

    record.was_alive = true

    if hp_values == nil then
        return
    end

    local previous = tonumber(record.hp)
    local current = hp_values.current
    local ignore_bonus = (state.ignored_bonus_until[key] or 0) >= state.frame

    if previous ~= nil and current < previous and not ignore_bonus then
        local delta = previous - current
        local damage_level = get_upgrade_level("damage")
        local multiplier = get_damage_multiplier(damage_level)
        if damage_level > 0 and multiplier > 1.0 then
            local bonus = delta * (multiplier - 1.0)
            local target = math.max(0.0, current - bonus)
            if set_current_hp(hp_values.hp, target) then
                current = target
                state.ignored_bonus_until[key] = state.frame + 2
                state.damage_status = string.format("x%.2f applied", multiplier)
            else
                state.damage_status = "Could not write enemy HP"
            end
        end

        apply_melee_splash(enemy, key, get_position(enemy), delta)
    end

    record.hp = current
end

local function process_player_damage_reduction()
    local player = get_player_context()
    local hp_values = get_hp_values(player)
    if hp_values == nil then
        state.player_last_hp = nil
        return
    end

    local current = hp_values.current
    local previous = state.player_last_hp
    local reduction = get_damage_reduction(get_upgrade_level("reduction"))

    if previous ~= nil and current < previous and reduction > 0.0 then
        local lost = previous - current
        local refund = lost * reduction
        local recovered = recover_hp(hp_values.hp, refund)
        if not recovered then
            recovered = set_current_hp(hp_values.hp, current + refund)
        end

        if recovered then
            current = current + refund
            state.reduction_status = string.format("%d%% refunded", math.floor(reduction * 100.0 + 0.5))
        else
            state.reduction_status = "Could not write player HP"
        end
    end

    state.player_last_hp = current
end

local function try_set_speed_on(target, multiplier)
    if target == nil then
        return nil
    end

    local methods = {
        "set_MoveSpeedRate",
        "set_MovementSpeedRate",
        "set_RunSpeedRate",
        "set_WalkSpeedRate",
        "set_SpeedRate",
        "set_MotionSpeedRate",
        "set_CombatSpeedRate",
        "set_MoveSpeedScale",
        "set_SpeedScale"
    }

    for _, method_name in ipairs(methods) do
        local ok = try_call_method(target, method_name, multiplier)
        if ok then
            return method_name
        end
    end

    local fields = {
        "_MoveSpeedRate",
        "<MoveSpeedRate>k__BackingField",
        "_MovementSpeedRate",
        "_RunSpeedRate",
        "_SpeedRate",
        "_MotionSpeedRate"
    }

    for _, field_name in ipairs(fields) do
        local ok = pcall(function()
            target:set_field(field_name, multiplier)
        end)
        if ok then
            return field_name
        end
    end

    return nil
end

local function apply_speed_upgrade()
    local level = get_upgrade_level("speed")
    if level <= 0 then
        return
    end

    local multiplier = 1.0 + (level * 0.08)
    local player = get_player_context()
    local targets = {
        player,
        get_game_object(player),
        get_transform(player)
    }

    for _, target in ipairs(targets) do
        local method = try_set_speed_on(target, multiplier)
        if method ~= nil then
            state.speed_status = string.format("x%.2f via %s", multiplier, method)
            return
        end
    end

    state.speed_status = "No speed API found"
end

local function set_pause_with_application(paused)
    local value = paused and 0.0 or 1.0
    local app = safe_call(function()
        return sdk.get_native_singleton("via.Application")
    end) or safe_call(function()
        return sdk.get_managed_singleton("via.Application")
    end)
    local app_type = safe_call(function()
        return sdk.find_type_definition("via.Application")
    end)

    if app == nil then
        return false
    end

    local methods = {
        "set_TimeScale",
        "set_TimeScale(System.Single)",
        "set_GameSpeed",
        "set_GameSpeed(System.Single)",
        "set_GlobalSpeed",
        "set_GlobalSpeed(System.Single)",
        "set_UpdateSpeed",
        "set_UpdateSpeed(System.Single)"
    }

    for _, method_name in ipairs(methods) do
        local ok = try_call_method(app, method_name, value)
        if ok then
            state.pause_method = "via.Application:" .. method_name
            return true
        end

        if app_type ~= nil then
            ok = pcall(function()
                sdk.call_native_func(app, app_type, method_name, value)
            end)
            if ok then
                state.pause_method = "native via.Application:" .. method_name
                return true
            end
        end
    end

    return false
end

local function set_pause_with_pause_manager(paused)
    local manager = safe_call(function()
        return sdk.get_managed_singleton("share.PauseManager")
    end)
    if manager == nil then
        return false
    end

    local methods = {
        "set_Pause",
        "setPause",
        "requestPause",
        "requestPause(System.Boolean)",
        "pause",
        "set_IsPause"
    }

    for _, method_name in ipairs(methods) do
        local ok = try_call_method(manager, method_name, paused)
        if ok then
            state.pause_method = "share.PauseManager:" .. method_name
            return true
        end
    end

    return false
end

local function set_game_paused(paused)
    if not shop.pause_on_open then
        return
    end

    if state.pause_applied == paused then
        return
    end

    local ok = set_pause_with_application(paused) or set_pause_with_pause_manager(paused)
    state.pause_applied = paused and ok

    if ok then
        state.pause_status = paused and ("Paused via " .. state.pause_method) or "Unpaused"
    else
        state.pause_status = "Pause API not found"
    end
end

local function set_shop_open(open)
    shop.open = open
    set_game_paused(open)
end

local function toggle_shop()
    set_shop_open(not shop.open)
end

local function update_keyboard()
    if keyboard_singleton == nil or keyboard_type == nil or hotkey_code == nil then
        return
    end

    local kb = safe_call(function()
        return sdk.call_native_func(keyboard_singleton, keyboard_type, "get_Device")
    end)
    if kb ~= nil and kb:isRelease(hotkey_code) then
        toggle_shop()
    end
end

local function update_system()
    state.frame = state.frame + 1
    init_types()

    for _, enemy in ipairs(collect_enemies()) do
        process_enemy(enemy)
    end

    process_player_damage_reduction()

    if state.frame % 30 == 0 then
        apply_speed_upgrade()
    end
end

local function draw_notification()
    if state.notification_frames <= 0 or state.notification == "" then
        return
    end

    draw.filled_rect(80, 80, 720, 46, 0xD0202020)
    draw.text(state.notification, 100, 94, 0xFFFFFFFF)
    state.notification_frames = state.notification_frames - 1
end

local function draw_shop_header()
    imgui.text("Currency: " .. tostring(data.currency) .. " kills")
    imgui.same_line()
    imgui.text("Total Kills: " .. tostring(data.total_kills))
    imgui.text("Hotkey: " .. shop.hotkey_name .. " toggle shop")
    imgui.text("Pause: " .. state.pause_status)
    imgui.separator()
end

local function draw_upgrade_row(def)
    local level = get_upgrade_level(def.id)
    local maxed = level >= def.max_level
    local cost = get_cost(def)

    imgui.text(def.title .. "  Lv." .. tostring(level) .. "/" .. tostring(def.max_level))
    imgui.text(def.value_text(level))

    if maxed then
        imgui.button("MAX##" .. def.id)
    else
        local label = "Buy (" .. tostring(cost) .. " kills)##" .. def.id
        if imgui.button(label) then
            buy_upgrade(def)
        end
        if not can_buy(def) then
            imgui.same_line()
            imgui.text("Need " .. tostring(math.max(0, cost - data.currency)) .. " more")
        end
    end

    imgui.separator()
end

local function draw_shop_window()
    if not shop.open then
        return
    end

    draw.filled_rect(0, 0, 2600, 1600, 0x66000000)

    local still_open = imgui.begin_window("RE4R Upgrade Shop", true, 0)
    if still_open then
        draw_shop_header()

        for _, def in ipairs(upgrade_defs) do
            draw_upgrade_row(def)
        end

        imgui.text("Runtime Status")
        imgui.text("Enemies tracked: " .. tostring(state.last_enemy_count))
        imgui.text("Speed: " .. state.speed_status)
        imgui.text("Damage: " .. state.damage_status)
        imgui.text("Reduction: " .. state.reduction_status)

        if imgui.button("Close Shop") then
            set_shop_open(false)
        end
    end
    imgui.end_window()

    if not still_open then
        set_shop_open(false)
    end
end

load_data()

re.on_application_entry("UpdateHID", function()
    init_types()
    update_keyboard()
end)

re.on_frame(function()
    update_system()
    draw_notification()
end)

re.on_draw_ui(function()
    draw_shop_window()
    if imgui.tree_node("RE4R Upgrade Shop") then
        if imgui.button(shop.open and "Close Shop" or "Open Shop") then
            toggle_shop()
        end
        imgui.text("Currency: " .. tostring(data.currency))
        imgui.text("Total Kills: " .. tostring(data.total_kills))
        imgui.tree_pop()
    end
end)
