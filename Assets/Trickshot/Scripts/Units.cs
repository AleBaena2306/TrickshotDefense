using System.Collections.Generic;
using UnityEngine;

namespace TrickshotDefense
{
    // ---------------------------------------------------------------- Robots
    public class Enemy : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool Dead { get; private set; }
        public bool Flying => Def.flying;
        public float Lane { get; private set; }

        private Battle battle;
        private Rig rig;
        private HealthBar bar;
        private float hitT, knock, anim, slow;
        private Renderer[] renderers;
        private float flash;

        public Vector3 HitPoint => transform.position + Vector3.up * (Flying ? 0.25f : 0.9f * Def.scale);
        public float X => transform.position.x;

        public static Enemy Create(Battle battle, EnemyDef def, float hpMul, float lane)
        {
            var go = Models.Spawn(def.model, battle.transform);
            var e = go.AddComponent<Enemy>();
            e.battle = battle;
            e.Def = def;
            e.MaxHp = e.Hp = def.hp * hpMul;
            e.Lane = lane;
            e.rig = new Rig(go);
            go.transform.position = new Vector3(World.SpawnX + Random.Range(0f, 0.6f), def.flying ? World.FlyHeight : 0f, lane);
            go.transform.rotation = Quaternion.Euler(0f, -90f, 0f);   // de frente hacia el club
            go.transform.localScale = Vector3.one * def.scale;
            e.bar = new HealthBar(go.transform, def.barHeight / def.scale + 0.25f, def.kind == EnemyKind.Boss ? 1.8f : 0.9f);
            e.renderers = go.GetComponentsInChildren<Renderer>();
            e.anim = Random.value * 10f;
            return e;
        }

        public void Hit(float damage, bool pierceArmor, Color color)
        {
            if (Dead) return;
            float dmg = pierceArmor ? damage : damage * (1f - Def.armor);
            Hp -= dmg;
            hitT = 1f;
            flash = 0.08f;
            if (Hp <= 0f) Die();
        }

        public void Knockback(float meters)
        {
            if (Dead || Def.kind == EnemyKind.Boss) { knock += meters * 0.15f; return; }
            knock += meters;
        }

        public void Slow(float seconds) { slow = Mathf.Max(slow, seconds); }

        private void Die()
        {
            Dead = true;
            battle.OnEnemyKilled(this);
            Puff.Burst(HitPoint, Ui.Hex(0x93A1B5), Def.kind == EnemyKind.Boss ? 18 : 7, Def.kind == EnemyKind.Boss ? 0.32f : 0.18f, 6f);
            Puff.Burst(HitPoint, Ui.Hex(0xFF4D4D), 3, 0.12f, 5f);
            Destroy(gameObject);
        }

        public void Tick(float dt, Camera cam)
        {
            if (Dead) return;
            anim += dt;
            float speed = Def.speed * (slow > 0f ? 0.5f : 1f);
            if (slow > 0f) slow -= dt;
            Vector3 p = transform.position;
            if (knock > 0f)
            {
                float k = Mathf.Min(knock, dt * 9f);
                p.x += k;
                knock -= k;
            }
            else p.x -= speed * dt;
            if (p.x > World.SpawnX + 1f) p.x = World.SpawnX + 1f;
            transform.position = p;

            hitT = Mathf.MoveTowards(hitT, 0f, dt * 6f);
            rig.AnimateRobot(Def.kind, anim, hitT);
            if (flash > 0f)
            {
                flash -= dt;
                if (flash > 0f) Models.TintAll(renderers, Color.white);
                else Models.ClearTint(renderers);
            }
            bar.Set(Hp / MaxHp, cam);

            if (p.x <= World.BaseReachX)
            {
                Dead = true;
                battle.OnEnemyReachedBase(this);
                Puff.Burst(HitPoint, Ui.Red, 6, 0.2f, 5f);
                Destroy(gameObject);
            }
        }
    }

    // ---------------------------------------------------------------- Deportistas
    public class Athlete : MonoBehaviour
    {
        public AthleteDef Def { get; private set; }
        public int Tier { get; private set; }               // 0, 1, 2
        public int Invested { get; private set; }
        public int SlotIndex { get; private set; }

        private Battle battle;
        private Rig rig;
        private float cooldown = 0.4f, attackT = -1f, celebrateT = -1f, anim;
        private bool released;
        private Enemy target;
        private Quaternion look;
        private Transform tierBadge;

        public float Range => Def.range * (Tier == 2 ? 1.1f : 1f);
        public float Damage => Def.damage * battle.Bal.tierDamageMul[Tier];
        public float Special => Tier == 2 ? Def.specialTier3 : Def.special;
        public int UpgradeCost => Tier >= 2 ? 0 : Mathf.RoundToInt(Def.cost * battle.Bal.upgradeCostMul[Tier] / 5f) * 5;
        public int SellValue => Mathf.RoundToInt(Invested * battle.Bal.sellRefund / 5f) * 5;

        public static Athlete Create(Battle battle, AthleteDef def, int slot, Color kit)
        {
            var go = Models.Spawn(def.model, battle.transform);
            var a = go.AddComponent<Athlete>();
            a.battle = battle;
            a.Def = def;
            a.SlotIndex = slot;
            a.Invested = def.cost;
            a.rig = new Rig(go);
            go.transform.position = World.Slots[slot] + Vector3.up * 0.18f;
            a.look = Quaternion.Euler(0f, 90f, 0f);
            go.transform.rotation = a.look;
            Models.Tint(go, "Kit", kit);
            a.anim = Random.value * 5f;
            a.celebrateT = 0f;
            return a;
        }

        public void SetKit(Color kit) => Models.Tint(gameObject, "Kit", kit);

        public void Upgrade(int paid)
        {
            if (Tier >= 2) return;
            Tier++;
            Invested += paid;
            celebrateT = 0f;
            transform.localScale = Vector3.one * (1f + Tier * 0.08f);
            Puff.Ring(transform.position, Def.color, 2.2f);
            Puff.Burst(transform.position + Vector3.up * 1.2f, Ui.Gold, 10, 0.14f, 5f);
        }

        public void Celebrate() { celebrateT = 0f; }

        private Enemy FindTarget()
        {
            Enemy best = null;
            float bestX = float.MaxValue, range = Range;
            Vector3 me = transform.position;
            foreach (var e in battle.Enemies)
            {
                if (e == null || e.Dead) continue;
                if (e.Flying && !Def.hitsFlyers) continue;
                if (World.FlatDistance(me, e.transform.position) > range) continue;
                if (e.X < bestX) { bestX = e.X; best = e; }   // el más cercano al club
            }
            return best;
        }

        public void Tick(float dt)
        {
            anim += dt;
            cooldown -= dt;
            if (target == null || target.Dead || World.FlatDistance(transform.position, target.transform.position) > Range)
                target = FindTarget();

            if (target != null)
            {
                Vector3 d = target.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) look = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 10f);

            if (attackT < 0f && cooldown <= 0f && target != null)
            {
                attackT = 0f;
                released = false;
                cooldown = Def.interval * battle.Bal.tierIntervalMul[Tier] * battle.TeamIntervalMul(Def.sport);
            }

            float attackDur = Mathf.Min(0.42f, Def.interval * 0.8f);
            if (attackT >= 0f)
            {
                attackT += dt / attackDur;
                if (!released && attackT >= 0.45f)
                {
                    released = true;
                    if (target != null && !target.Dead) Projectile.Fire(battle, this, target);
                }
                if (attackT >= 1f) attackT = -1f;
            }
            if (celebrateT >= 0f) { celebrateT += dt / 0.9f; if (celebrateT >= 1f) celebrateT = -1f; }
            rig.AnimateAthlete(Def.sport, anim, attackT, celebrateT);
        }

        public Vector3 HandPoint => transform.position + Vector3.up * 1.1f + transform.forward * 0.4f;
    }

    // ---------------------------------------------------------------- Proyectiles
    public class Projectile : MonoBehaviour
    {
        private Battle battle;
        private Athlete owner;
        private Sport sport;
        private Enemy target;
        private Vector3 lastTargetPos, start, end;
        private float speed, damage, special, t, flight;
        private int bouncesLeft;
        private readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        private bool pierceArmor, rolling;
        private Vector3 rollDir;
        private float rollLeft;

        private static string BallModel(Sport s)
        {
            switch (s)
            {
                case Sport.Soccer: return "ball_soccer";
                case Sport.Tennis: return "ball_tennis";
                case Sport.Basket: return "ball_basket";
                case Sport.Bowling: return "ball_bowling";
                default: return "ball_pingpong";
            }
        }

        public static void Fire(Battle battle, Athlete owner, Enemy target)
        {
            var def = owner.Def;
            var go = Models.Spawn(BallModel(def.sport), battle.transform);
            var p = go.AddComponent<Projectile>();
            p.battle = battle;
            p.owner = owner;
            p.sport = def.sport;
            p.target = target;
            p.speed = def.projectileSpeed;
            p.damage = owner.Damage;
            p.special = owner.Special;
            p.pierceArmor = def.ignoresArmor;
            p.start = owner.HandPoint;
            p.lastTargetPos = target.HitPoint;
            go.transform.position = p.start;
            if (def.sport == Sport.Tennis) p.bouncesLeft = Mathf.RoundToInt(p.special);
            if (def.sport == Sport.Basket)
            {
                // Tiro en parábola al punto donde estará el robot
                float dist = World.FlatDistance(p.start, target.transform.position);
                p.flight = Mathf.Max(0.45f, dist / p.speed);
                Vector3 lead = target.transform.position + Vector3.left * target.Def.speed * p.flight;
                p.end = new Vector3(lead.x, 0.2f, lead.z);
            }
            if (def.sport == Sport.Bowling)
            {
                p.end = new Vector3(target.transform.position.x, 0.25f, target.transform.position.z);
                p.flight = Mathf.Max(0.25f, World.FlatDistance(p.start, p.end) / p.speed);
            }
            Sfx.Play("throw_" + def.sport.ToString().ToLowerInvariant(), 0.6f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (sport)
            {
                case Sport.Basket: TickLob(dt); break;
                case Sport.Bowling: TickRoll(dt); break;
                default: TickHoming(dt); break;
            }
        }

        private void TickHoming(float dt)
        {
            if (target != null && !target.Dead) lastTargetPos = target.HitPoint;
            Vector3 pos = transform.position;
            Vector3 to = lastTargetPos - pos;
            float step = speed * dt;
            transform.Rotate(Vector3.right, 900f * dt, Space.Self);
            if (to.magnitude <= step + 0.15f)
            {
                transform.position = lastTargetPos;
                if (target != null && !target.Dead) OnHoming(target);
                else Destroy(gameObject);
                return;
            }
            transform.position = pos + to.normalized * step;
        }

        private void OnHoming(Enemy e)
        {
            float dmg = damage;
            if (sport == Sport.PingPong && e.Flying) dmg *= special;
            e.Hit(dmg, pierceArmor, owner != null ? owner.Def.color : Color.white);
            battle.OnHit(e.HitPoint, sport);
            if (sport == Sport.Soccer) e.Knockback(special);
            if (sport == Sport.Tennis && bouncesLeft > 0)
            {
                hit.Add(e);
                bouncesLeft--;
                damage *= 0.85f;
                Enemy next = null;
                float best = 4.5f;
                foreach (var o in battle.Enemies)
                {
                    if (o == null || o.Dead || hit.Contains(o)) continue;
                    float d = Vector3.Distance(o.HitPoint, transform.position);
                    if (d < best) { best = d; next = o; }
                }
                if (next != null) { target = next; lastTargetPos = next.HitPoint; return; }
            }
            Destroy(gameObject);
        }

        private void TickLob(float dt)
        {
            t += dt / flight;
            float k = Mathf.Clamp01(t);
            Vector3 flat = Vector3.Lerp(start, end, k);
            flat.y = Mathf.Lerp(start.y, end.y, k) + Mathf.Sin(k * Mathf.PI) * (2.5f + flight * 1.5f);
            transform.position = flat;
            transform.Rotate(Vector3.right, 500f * dt, Space.Self);
            if (k >= 1f)
            {
                float radius = special;
                foreach (var e in battle.Enemies.ToArray())
                {
                    if (e == null || e.Dead || e.Flying) continue;
                    if (World.FlatDistance(e.transform.position, end) <= radius) e.Hit(damage, true, Color.white);
                }
                Puff.Ring(end, Ui.Hex(0xF08A24), radius * 2f);
                battle.OnHit(end, sport);
                battle.Shake(0.12f);
                Destroy(gameObject);
            }
        }

        private void TickRoll(float dt)
        {
            if (!rolling)
            {
                // Vuela hasta la pista y después rueda hacia los robots que llegan
                t += dt / flight;
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(start, end, k);
                p.y = Mathf.Lerp(start.y, end.y, k) + Mathf.Sin(k * Mathf.PI) * 0.8f;
                transform.position = p;
                if (k >= 1f)
                {
                    rolling = true;
                    rollDir = Vector3.right;
                    rollLeft = owner != null ? owner.Range : 10f;
                    Puff.Burst(end, Ui.Hex(0xC9B07A), 4, 0.12f, 3f);
                }
                return;
            }
            float step = speed * dt;
            transform.position += rollDir * step;
            transform.Rotate(Vector3.forward, -720f * dt, Space.World);
            rollLeft -= step;
            float width = special;
            foreach (var e in battle.Enemies.ToArray())
            {
                if (e == null || e.Dead || e.Flying || hit.Contains(e)) continue;
                Vector3 ep = e.transform.position;
                if (Mathf.Abs(ep.x - transform.position.x) < 0.6f && Mathf.Abs(ep.z - transform.position.z) < width + 0.4f)
                {
                    hit.Add(e);
                    e.Hit(damage, pierceArmor, Color.white);
                    e.Slow(0.6f);
                    battle.OnHit(e.HitPoint, sport);
                }
            }
            if (rollLeft <= 0f || transform.position.x > World.SpawnX + 2f) Destroy(gameObject);
        }
    }
}
