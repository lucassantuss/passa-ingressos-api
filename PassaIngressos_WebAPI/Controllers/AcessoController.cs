﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassaIngressos_WebAPI.Database;
using PassaIngressos_WebAPI.Entity;
using PassaIngressos_WebAPI.Dto;
using PassaIngressos_WebAPI.Util;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace PassaIngressos_WebAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AcessoController : ControllerBase
    {
        #region Contexto e Variáveis

        private readonly DbPassaIngressos _dbPassaIngressos;
        private readonly IConfiguration _configuration;

        public AcessoController(IConfiguration configuration, DbPassaIngressos _context)
        {
            _configuration = configuration;
            _dbPassaIngressos = _context;
        }

        #endregion

        #region Perfil

        // Método para listar todos os Perfis
        [Authorize]
        [HttpGet("ListarPerfis")]
        public async Task<IActionResult> ListarPerfis()
        {
            var listaPerfis = await _dbPassaIngressos.Perfis.ToListAsync();

            if (listaPerfis == null || !listaPerfis.Any())
                return NotFound("Não foi encontrado nenhum perfil.");

            return Ok(listaPerfis);
        }

        // Método para criar Perfil
        [Authorize]
        [HttpPost("CriarPerfil")]
        public async Task<IActionResult> CriarPerfil([FromBody] PerfilDto perfilDto)
        {
            if (perfilDto == null || string.IsNullOrWhiteSpace(perfilDto.NomePerfil))
                return BadRequest("Nome do perfil é obrigatório.");

            var perfil = new Perfil
            {
                NomePerfil = perfilDto.NomePerfil,
                DescricaoPerfil = perfilDto.DescricaoPerfil
            };

            _dbPassaIngressos.Perfis.Add(perfil);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok(perfil);
        }

        // Método para alterar Perfil
        [Authorize]
        [HttpPut("AlterarPerfil/{idPerfil}")]
        public async Task<IActionResult> AlterarPerfil(int idPerfil, [FromBody] PerfilDto perfilAtualizado)
        {
            if (perfilAtualizado == null || string.IsNullOrWhiteSpace(perfilAtualizado.NomePerfil))
                return BadRequest("Nome do perfil é obrigatório.");

            var perfil = await _dbPassaIngressos.Perfis.FindAsync(idPerfil);

            if (perfil == null)
                return NotFound("Perfil não encontrado.");

            perfil.NomePerfil = perfilAtualizado.NomePerfil;
            perfil.DescricaoPerfil = perfilAtualizado.DescricaoPerfil;

            await _dbPassaIngressos.SaveChangesAsync();

            return Ok(perfil);
        }

        // Método para remover Perfil
        [Authorize]
        [HttpDelete("RemoverPerfil/{idPerfil}")]
        public async Task<IActionResult> RemoverPerfil(int idPerfil)
        {
            var perfil = await _dbPassaIngressos.Perfis.FindAsync(idPerfil);

            if (perfil == null)
                return NotFound("Perfil não encontrado.");

            _dbPassaIngressos.Perfis.Remove(perfil);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Perfil removido com sucesso.");
        }

        // Método para pesquisar todos os usuários do Perfil
        [Authorize]
        [HttpGet("UsuariosDoPerfil/{idPerfil}")]
        public async Task<IActionResult> UsuariosDoPerfil(int idPerfil)
        {
            var listaIdsUsuarios = await _dbPassaIngressos.UsuarioPerfis
                                                          .Where(up => up.IdPerfil == idPerfil)
                                                          .Select(up => up.IdUsuario)
                                                          .ToListAsync();

            if (listaIdsUsuarios == null || !listaIdsUsuarios.Any())
                return NotFound("Nenhum usuário encontrado para esse perfil.");

            var usuarios = await _dbPassaIngressos.Usuarios
                                 .Where(xs => listaIdsUsuarios.Contains(xs.IdUsuario))
                                 .ToListAsync();

            var usuarioDtos = usuarios.Select(u => new UsuariosPerfilDto
            {
                IdUsuario = u.IdUsuario,
                Login = u.Login,
            }).ToList();

            return Ok(usuarioDtos);
        }

        #endregion

        #region Usuário

        // Método para criar usuário
        [HttpPost("CriarUsuario")]
        public async Task<IActionResult> CriarUsuario([FromBody] UsuarioDto usuarioDto)
        {
            if (usuarioDto == null || string.IsNullOrWhiteSpace(usuarioDto.Login) ||
                string.IsNullOrWhiteSpace(usuarioDto.Senha) || 
                string.IsNullOrWhiteSpace(usuarioDto.NomePessoa))
            {
                return BadRequest("Dados do usuário são obrigatórios.");
            }

            ValidacaoHelper ValidacaoHelper = new ValidacaoHelper();

            if (!ValidacaoHelper.IsValidCPF(usuarioDto.CPF))
                return BadRequest("CPF inválido.");

            // Cria Pessoa que será associada ao Usuário
            var pessoa = new Pessoa()
            {
                Nome = usuarioDto.NomePessoa,
                CPF = usuarioDto.CPF,
                RG = usuarioDto.RG,
                DataNascimento = usuarioDto.DataNascimento,
                IdArquivoFoto = usuarioDto.IdArquivoFoto ?? null,
                IdTgSexo = usuarioDto.IdTgSexo ?? null
            };

            _dbPassaIngressos.Pessoas.Add(pessoa);
            await _dbPassaIngressos.SaveChangesAsync();

            // Cria Usuário
            var usuario = new Usuario()
            {
                Login = usuarioDto.Login,
                Senha = BCrypt.Net.BCrypt.HashPassword(usuarioDto.Senha), // Criptografa a senha
                IdPessoa = pessoa.IdPessoa
            };

            _dbPassaIngressos.Usuarios.Add(usuario);
            await _dbPassaIngressos.SaveChangesAsync();

            // Adicionar Usuário no Perfil USER
            var usuarioPerfil = new UsuarioPerfil()
            {
                IdUsuario = usuario.IdUsuario,
                IdPerfil = 2 // Perfil USER
            };

            _dbPassaIngressos.UsuarioPerfis.Add(usuarioPerfil);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Usuário criado com sucesso!");
        }

        // Método para redefinir senha
        [HttpPut("RedefinirSenha/{idUsuario}")]
        public async Task<IActionResult> RedefinirSenha(int idUsuario, [FromBody] RedefineSenhaDto redefineSenhaDto)
        {
            if (redefineSenhaDto == null || string.IsNullOrWhiteSpace(redefineSenhaDto.Senha))
                return BadRequest("Senha é obrigatória.");

            var usuario = await _dbPassaIngressos.Usuarios.FindAsync(idUsuario);

            if (usuario == null)
                return NotFound("Usuário não encontrado.");

            usuario.Senha = BCrypt.Net.BCrypt.HashPassword(redefineSenhaDto.Senha);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Senha redefinida com sucesso.");
        }

        // Método para validar usuário e realizar o login
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Login) || string.IsNullOrWhiteSpace(loginDto.Senha))
                return BadRequest("Login e senha são obrigatórios.");

            var usuario = await _dbPassaIngressos.Usuarios
                                .SingleOrDefaultAsync(u => u.Login == loginDto.Login &&
                                                           u.Senha == loginDto.Senha);

            if (usuario == null)
                return Unauthorized("Login ou senha inválidos.");

            // Gerando o Token JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSecretKey = _configuration["Jwt_ChaveSecreta_PassaIngressos"];
            var key = Encoding.ASCII.GetBytes(jwtSecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                    new Claim(ClaimTypes.Name, usuario.Login.ToString()),
                }),
                Expires = DateTime.UtcNow.AddHours(3),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenJWT = tokenHandler.WriteToken(token);

            return Ok(new { Token = tokenJWT });
        }

        // Método para pesquisar usuário por login
        [Authorize]
        [HttpGet("PesquisarUsuarioPorLogin/{login}")]
        public async Task<IActionResult> PesquisarUsuarioPorLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                return BadRequest("Login é obrigatório.");

            var usuario = await _dbPassaIngressos.Usuarios
                                .Include(u => u.Pessoa)
                                .SingleOrDefaultAsync(u => u.Login == login);

            if (usuario == null)
                return NotFound("Usuário não encontrado.");

            return Ok(usuario);
        }

        // Método para excluir conta
        [Authorize]
        [HttpDelete("ExcluirConta/{idUsuario}")]
        public async Task<IActionResult> ExcluirConta(int idUsuario)
        {
            var usuario = await _dbPassaIngressos.Usuarios.FindAsync(idUsuario);

            if (usuario == null)
                return NotFound("Usuário não encontrado.");

            var usuarioPerfis = await _dbPassaIngressos.UsuarioPerfis
                                                       .Where(xs => xs.IdUsuario == usuario.IdUsuario)
                                                       .ToListAsync();

            if (usuarioPerfis == null)
                return NotFound("Perfis do usuário não encontrados.");

            var usuarioPessoa = await _dbPassaIngressos.Pessoas
                                                       .Where(xs => xs.IdPessoa == usuario.IdPessoa)
                                                       .FirstOrDefaultAsync();

            if (usuarioPessoa == null)
                return NotFound("Pessoa não encontrada.");

            _dbPassaIngressos.Pessoas.Remove(usuarioPessoa);
            _dbPassaIngressos.UsuarioPerfis.RemoveRange(usuarioPerfis);
            _dbPassaIngressos.Usuarios.Remove(usuario);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Conta excluída com sucesso.");
        }

        // Método para pesquisar Perfis associados ao IdUsuario
        [Authorize]
        [HttpGet("PerfisDoUsuario/{idUsuario}")]
        public async Task<IActionResult> PerfisDoUsuario(int idUsuario)
        {
            var listaIdsPerfis = await _dbPassaIngressos.UsuarioPerfis
                                .Where(up => up.IdUsuario == idUsuario)
                                .Select(up => up.IdPerfil)
                                .ToListAsync();

            if (listaIdsPerfis == null || !listaIdsPerfis.Any())
                return NotFound("Nenhum perfil associado a este usuário.");

            var perfis = await _dbPassaIngressos.Perfis
                                 .Where(xs => listaIdsPerfis.Contains(xs.IdPerfil))
                                 .ToListAsync();

            return Ok(perfis);
        }

        // Método para adicionar Perfil ao IdUsuario
        [Authorize]
        [HttpPost("AdicionarPerfilAoUsuario/{idUsuario}")]
        public async Task<IActionResult> AdicionarPerfilAoUsuario(int idUsuario, [FromBody] PerfilUsuarioDto adicionaPerfilDto)
        {
            if (adicionaPerfilDto == null)
                return BadRequest("Dados do perfil são obrigatórios.");

            var usuario = await _dbPassaIngressos.Usuarios.FindAsync(idUsuario);
            var perfil = await _dbPassaIngressos.Perfis.FindAsync(adicionaPerfilDto.IdPerfil);

            if (usuario == null)
                return NotFound("Usuário não encontrado.");

            if (perfil == null)
                return NotFound("Perfil não encontrado.");

            var usuarioPerfil = new UsuarioPerfil {
                IdUsuario = idUsuario,
                IdPerfil = adicionaPerfilDto.IdPerfil
            };

            var usuarioPerfilExistente = await _dbPassaIngressos.UsuarioPerfis
                                               .Where(xs => xs.IdUsuario == usuarioPerfil.IdUsuario)
                                               .Where(xs => xs.IdPerfil == usuarioPerfil.IdPerfil)
                                               .FirstOrDefaultAsync();

            if (usuarioPerfilExistente != null)
                return Unauthorized("Perfil já está associado ao usuário informado.");

            _dbPassaIngressos.UsuarioPerfis.Add(usuarioPerfil);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Perfil adicionado ao usuário com sucesso.");
        }

        // Método para remover Perfil do IdUsuario
        [Authorize]
        [HttpDelete("RemoverPerfilDoUsuario/{idUsuario}")]
        public async Task<IActionResult> RemoverPerfilDoUsuario(int idUsuario, [FromBody] PerfilUsuarioDto removePerfilDto)
        {
            if (removePerfilDto == null)
                return BadRequest("Dados do perfil são obrigatórios.");

            var usuario = await _dbPassaIngressos.Usuarios.FindAsync(idUsuario);
            var perfil = await _dbPassaIngressos.Perfis.FindAsync(removePerfilDto.IdPerfil);

            if (usuario == null)
                return NotFound("Usuário não encontrado.");

            if (perfil == null)
                return NotFound("Perfil não encontrado.");

            var usuarioPerfil = await _dbPassaIngressos.UsuarioPerfis
                                          .Where(xs => xs.IdUsuario == idUsuario)
                                          .Where(xs => xs.IdPerfil == removePerfilDto.IdPerfil)
                                          .FirstOrDefaultAsync();

            if (usuarioPerfil == null)
                return NotFound("Perfil não está associado ao usuário informado.");

            _dbPassaIngressos.UsuarioPerfis.Remove(usuarioPerfil);
            await _dbPassaIngressos.SaveChangesAsync();

            return Ok("Perfil removido do usuário com sucesso.");
        }

        #endregion
    }
}