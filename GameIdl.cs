using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using SolDungeons;
using SolDungeons.Program;
using SolDungeons.Errors;
using SolDungeons.Accounts;

namespace SolDungeons
{
    namespace Accounts
    {
        public partial class User
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 17022084798167872927UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{159, 117, 95, 227, 239, 151, 58, 236};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "TfwwBiNJtao";
            public PublicKey Authority { get; set; }

            public string Username { get; set; }

            public byte CurrentCharacterId { get; set; }

            public static User Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                User result = new User();
                result.Authority = _data.GetPubKey(offset);
                offset += 32;
                offset += _data.GetBorshString(offset, out var resultUsername);
                result.Username = resultUsername;
                result.CurrentCharacterId = _data.GetU8(offset);
                offset += 1;
                return result;
            }
        }

        public partial class UserCharacter
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 6663967752651742160UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{208, 27, 119, 35, 93, 43, 123, 92};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "bouHCPMrFuu";
            public byte MintId { get; set; }

            public bool Locked { get; set; }

            public ulong LastLockedTime { get; set; }

            public static UserCharacter Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                UserCharacter result = new UserCharacter();
                result.MintId = _data.GetU8(offset);
                offset += 1;
                result.Locked = _data.GetBool(offset);
                offset += 1;
                result.LastLockedTime = _data.GetU64(offset);
                offset += 8;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum SolDungeonsErrorKind : uint
        {
            WrongAuthority = 6000U
        }
    }

    public partial class SolDungeonsClient : TransactionalBaseClient<SolDungeonsErrorKind>
    {
        public SolDungeonsClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>> GetUsersAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = User.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>(res);
            List<User> resultingAccounts = new List<User>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => User.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserCharacter>>> GetUserCharactersAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = UserCharacter.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserCharacter>>(res);
            List<UserCharacter> resultingAccounts = new List<UserCharacter>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => UserCharacter.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserCharacter>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<User>> GetUserAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<User>(res);
            var resultingAccount = User.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<User>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<UserCharacter>> GetUserCharacterAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<UserCharacter>(res);
            var resultingAccount = UserCharacter.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<UserCharacter>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeUserAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, User> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                User parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = User.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribeUserCharacterAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, UserCharacter> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                UserCharacter parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = UserCharacter.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitializeUserAsync(InitializeUserAccounts accounts, string username, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.InitializeUser(accounts, username, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendUpdateUsernameAsync(UpdateUsernameAccounts accounts, string username, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.UpdateUsername(accounts, username, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendAssignPlayerCharacterAsync(AssignPlayerCharacterAccounts accounts, byte characterMintId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.AssignPlayerCharacter(accounts, characterMintId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendLockCurrentUserCharacterAsync(LockCurrentUserCharacterAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.LockCurrentUserCharacter(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendAddTokenAsync(AddTokenAccounts accounts, ulong amount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.AddToken(accounts, amount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendReduceTokenAsync(ReduceTokenAccounts accounts, ulong amount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolDungeonsProgram.ReduceToken(accounts, amount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<SolDungeonsErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<SolDungeonsErrorKind>>{{6000U, new ProgramError<SolDungeonsErrorKind>(SolDungeonsErrorKind.WrongAuthority, "Wrong Authority")}, };
        }
    }

    namespace Program
    {
        public class InitializeUserAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class UpdateUsernameAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey SessionToken { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class AssignPlayerCharacterAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey UserCharacter { get; set; }

            public PublicKey SessionToken { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class LockCurrentUserCharacterAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey UserCharacter { get; set; }

            public PublicKey SessionToken { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class AddTokenAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey VaultPda { get; set; }

            public PublicKey VaultAta { get; set; }

            public PublicKey UserAta { get; set; }

            public PublicKey GameToken { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SessionToken { get; set; }

            public PublicKey AssociatedTokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class ReduceTokenAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey SignerWallet { get; set; }

            public PublicKey User { get; set; }

            public PublicKey VaultPda { get; set; }

            public PublicKey VaultAta { get; set; }

            public PublicKey UserAta { get; set; }

            public PublicKey GameToken { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SessionToken { get; set; }

            public PublicKey AssociatedTokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public static class SolDungeonsProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeUser(InitializeUserAccounts accounts, string username, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(18313459337071759727UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(username, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction UpdateUsername(UpdateUsernameAccounts accounts, string username, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(18147365723531208681UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(username, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction AssignPlayerCharacter(AssignPlayerCharacterAccounts accounts, byte characterMintId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserCharacter, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(18030146948214668928UL, offset);
                offset += 8;
                _data.WriteU8(characterMintId, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction LockCurrentUserCharacter(LockCurrentUserCharacterAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserCharacter, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(14638810002693361809UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction AddToken(AddTokenAccounts accounts, ulong amount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.VaultPda, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.VaultAta, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserAta, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.GameToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(3766188206372618221UL, offset);
                offset += 8;
                _data.WriteU64(amount, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ReduceToken(ReduceTokenAccounts accounts, ulong amount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.SignerWallet, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.VaultPda, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.VaultAta, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserAta, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.GameToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(15217381811324258862UL, offset);
                offset += 8;
                _data.WriteU64(amount, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}