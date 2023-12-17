use anchor_lang::prelude::*;
use session_keys::{SessionError, SessionToken, session_auth_or, Session};
use anchor_spl::{token::{Transfer, TokenAccount, Token, Mint}, associated_token::AssociatedToken};

declare_id!("CW7thTzLfzZop6TtHrD4FgjcJzxNMiscHRR9XrdW4T14");

pub const PLAYER_SEED: &[u8] = b"PLAYER";
pub const VAULT_SEED: &[u8] = b"VAULT";
pub const LOCKIN_TIME: u64 = 1800;

#[program]
pub mod sol_dungeons 
{
    use super::*;

    pub fn initialize_user(ctx: Context<InitializeUser>, username: String) -> Result<()> 
    {
        let user = &mut ctx.accounts.user;

        user.username = username;
        user.authority = ctx.accounts.signer.key();

        Ok(())
    }

    #[session_auth_or(ctx.accounts.user.authority.key() == ctx.accounts.signer.key(), GameErrorCode::WrongAuthority)]
    pub fn update_username(ctx: Context<UpdateUsername>, username: String) -> Result<()> 
    {
        let user = &mut ctx.accounts.user;

        user.username = username;

        Ok(())
    }

    #[session_auth_or(ctx.accounts.user.authority.key() == ctx.accounts.signer.key(), GameErrorCode::WrongAuthority)]
    pub fn assign_player_character(ctx: Context<AssignUserCharacter>, character_mint_id: u8) -> Result<()> 
    {
        let user = &mut ctx.accounts.user;
        let user_character = &mut ctx.accounts.user_character;

        if user_character.locked
        {
            let current_time = Clock::get().unwrap().unix_timestamp as u64;
            let time_passed = current_time - user_character.last_locked_time;

            if time_passed > LOCKIN_TIME
            {
                user.current_character_id = user_character.mint_id.clone();
                user_character.locked = false;
            }
        }
        else 
        {
            user_character.mint_id = character_mint_id;
            user_character.last_locked_time = 0;
            user_character.locked = false;
            user.current_character_id = user_character.mint_id.clone();
        }

        Ok(())
    }

    #[session_auth_or(ctx.accounts.user.authority.key() == ctx.accounts.signer.key(), GameErrorCode::WrongAuthority)]
    pub fn lock_current_user_character(ctx: Context<LockCurrentUserCharacter>) -> Result<()> 
    {
        let user_character = &mut ctx.accounts.user_character;

        user_character.last_locked_time = Clock::get().unwrap().unix_timestamp as u64;
        user_character.locked = true;

        Ok(())
    }

    #[session_auth_or(ctx.accounts.user.authority.key() == ctx.accounts.signer.key(), GameErrorCode::WrongAuthority)]
    pub fn add_token(ctx: Context<AddToken>, amount: u64) -> Result<()>
    {
        let transfer_accounts = Transfer {
            from: ctx.accounts.vault_ata.to_account_info(),
            to: ctx.accounts.user_ata.to_account_info(),
            authority: ctx.accounts.vault_pda.to_account_info(),
        };

        let seeds:&[&[u8]] = &[
            VAULT_SEED,
            &[ctx.bumps.vault_pda]
        ];
        let signer = &[&seeds[..]];

        let cpi_ctx = CpiContext::new_with_signer(
            ctx.accounts.token_program.to_account_info(),
            transfer_accounts,
            signer
        );

        anchor_spl::token::transfer(cpi_ctx, amount)?;

        Ok(())
    }

    #[session_auth_or(ctx.accounts.user.authority.key() == ctx.accounts.signer.key(), GameErrorCode::WrongAuthority)]
    pub fn reduce_token(ctx: Context<ReduceToken>, amount: u64) -> Result<()>
    {
        let transfer_accounts = Transfer {
            from: ctx.accounts.user_ata.to_account_info(),
            to: ctx.accounts.vault_ata.to_account_info(),
            authority: ctx.accounts.signer_wallet.to_account_info(),
        };

        let cpi_ctx = CpiContext::new(
            ctx.accounts.token_program.to_account_info(),
            transfer_accounts
        );

        anchor_spl::token::transfer(cpi_ctx, amount)?;

        Ok(())
    }
}

#[derive(Accounts)]
#[instruction(username: String)]
pub struct InitializeUser<'info> 
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(init, payer = signer, seeds=[PLAYER_SEED, signer.key().as_ref()], bump, space = 8 + 32 + 1 + 4 + username.len())]
    pub user: Account<'info, User>,

    pub system_program: Program<'info, System>
}

#[derive(Accounts, Session)]
#[instruction(character_mint: u8)]
pub struct AssignUserCharacter<'info>
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(mut, seeds=[PLAYER_SEED, user.authority.key().as_ref()], bump)]
    pub user: Account<'info, User>,

    #[account(init_if_needed, payer = signer, seeds=[character_mint.to_string().as_ref(), user.authority.key().as_ref()], bump, space = 8 + 1 + 8 + 1)]
    pub user_character: Account<'info, UserCharacter>,

    #[session(signer = signer, authority = user.authority.key())]
    pub session_token: Option<Account<'info, SessionToken>>,

    pub system_program: Program<'info, System>
}

#[derive(Accounts, Session)]
#[instruction(username: String)]
pub struct UpdateUsername<'info>
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(mut, seeds=[PLAYER_SEED, user.authority.key().as_ref()], bump, realloc = 8 + 32 + 1 + 4 + username.len(), realloc::payer = signer, realloc::zero = false)]
    pub user: Account<'info, User>,

    #[session(signer = signer, authority = user.authority.key())]
    pub session_token: Option<Account<'info, SessionToken>>,

    pub system_program: Program<'info, System>
}

#[derive(Accounts, Session)]
pub struct LockCurrentUserCharacter<'info> 
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(seeds=[PLAYER_SEED, user.authority.key().as_ref()], bump)]
    pub user: Account<'info, User>,

    #[account(mut, seeds=[user.current_character_id.to_string().as_ref(), user.authority.key().as_ref()], bump)]
    pub user_character: Account<'info, UserCharacter>,

    #[session(signer = signer, authority = user.authority.key())]
    pub session_token: Option<Account<'info, SessionToken>>,

    pub system_program: Program<'info, System>
}

#[derive(Accounts, Session)]
pub struct AddToken<'info>
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(mut, seeds = [PLAYER_SEED, user.authority.key().as_ref()], bump)]
    pub user: Account<'info, User>,
  
    ///CHECK:
    #[account(seeds=[VAULT_SEED], bump)]
    pub vault_pda: AccountInfo<'info>,

    #[account(mut, associated_token::mint = game_token, associated_token::authority = vault_pda)]
    pub vault_ata: Account<'info, TokenAccount>,

    #[account(mut, associated_token::mint = game_token, associated_token::authority = user.authority.key())]
    pub user_ata: Account<'info, TokenAccount>,

    pub game_token: Account<'info, Mint>,

    pub token_program: Program<'info, Token>,

    #[session(signer = signer, authority = user.authority.key())]
    pub session_token: Option<Account<'info, SessionToken>>,

    pub associated_token_program: Program<'info, AssociatedToken>,
    pub system_program: Program<'info, System>
}

#[derive(Accounts, Session)]
pub struct ReduceToken<'info>
{
    #[account(mut)]
    pub signer: Signer<'info>,

    #[account(mut)]
    pub signer_wallet: Signer<'info>,

    #[account(mut, seeds = [PLAYER_SEED, user.authority.key().as_ref()], bump)]
    pub user: Account<'info, User>,
  
    ///CHECK:
    #[account(seeds=[VAULT_SEED], bump)]
    pub vault_pda: AccountInfo<'info>,

    #[account(mut, associated_token::mint = game_token, associated_token::authority = vault_pda)]
    pub vault_ata: Account<'info, TokenAccount>,

    #[account(mut, associated_token::mint = game_token, associated_token::authority = user.authority.key())]
    pub user_ata: Account<'info, TokenAccount>,

    pub game_token: Account<'info, Mint>,

    pub token_program: Program<'info, Token>,

    #[session(signer = signer, authority = user.authority.key())]
    pub session_token: Option<Account<'info, SessionToken>>,

    pub associated_token_program: Program<'info, AssociatedToken>,
    pub system_program: Program<'info, System>
}

#[account]
pub struct User
{
    pub authority: Pubkey,
    pub username: String,
    pub current_character_id: u8
}

#[account]
pub struct UserCharacter
{
    pub mint_id: u8,
    pub locked: bool,
    pub last_locked_time: u64
}

#[error_code]
pub enum GameErrorCode 
{
    #[msg("Wrong Authority")]
    WrongAuthority,
}