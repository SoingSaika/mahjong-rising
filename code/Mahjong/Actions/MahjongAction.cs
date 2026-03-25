using System.Threading.Tasks;

namespace MahjongRising.code.Mahjong.Actions;

public abstract class MahjongAction
{
    public virtual Task OnDraw(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnDiscard(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnChi(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnPeng(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnGang(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnHu(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnTurnStart(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnTurnEnd(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnRoundStart(MahjongActionContext context) => Task.CompletedTask;

    public virtual Task OnRoundEnd(MahjongActionContext context) => Task.CompletedTask;
}