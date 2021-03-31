using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using condominioApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MongoDB.Driver;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
/*
    Gera token utilizando a chave Secret no arquivo Settings
*/
namespace condominioApi.Services
{

    public class UserService
    {
        private string URL = "https://localhost:5001/";
        private double _timeExpiredTokenEMAIL = 0.5;
        public BsonDocument RetornaUserAdm(UserAdm user)
        {
            if (user.image == null)
            {
                user.image = new byte[1];
            }
            return new BsonDocument{
                    {"_id", ObjectId.GenerateNewId()},
                    {"email", user.email.ToLower()},
                    {"password", passwordToHash(password: user.password)},
                    {"estado", user.estado},
                    {"cidade", user.cidade},
                    {"endereco", user.endereco},
                    {"nameCondominio", user.nameCondominio},
                    {"image", user.image},
                    {"verificado" , false},
                    {"role", "Administrator"},
                    {"datacreate", DateTimeOffset.Now.ToUnixTimeSeconds()}
                };
        }
        public BsonDocument RetornaUserRef(UserGenericLogin user)
        {
            if (user.GetType() == new UserAdm().GetType())
            {
                return new BsonDocument{
                    {"_id", ObjectId.GenerateNewId()},
                    {"email", user.email.ToLower()},
                    {"databaseName", RemoverCaracterEspecial(user.nameCondominio)},
                    {"nameCondominio", user.nameCondominio},
                    {"role", "Administrator"}

                };
            }
            else if (user.GetType() == new UserPorteiro().GetType())
            {
                return new BsonDocument{
                    {"_id", ObjectId.GenerateNewId()},
                    {"email", user.email.ToLower()},
                    {"databaseName", RemoverCaracterEspecial(user.nameCondominio)},
                    {"nameCondominio", user.nameCondominio},
                    {"role", "Porteiro"}

                };
            }
            return null;

        }
        public BsonDocument RetornaUserPorteiro(UserPorteiro user)
        {
            if (user.image == null)
            {
                user.image = new byte[1];
            }
            return new BsonDocument{
                    {"_id", ObjectId.GenerateNewId()},
                    {"email", user.email.ToLower()},
                    {"password", passwordToHash(password: user.password)},
                    {"nameCondominio", user.nameCondominio },
                    {"nome", user.nome.ToLower() },
                    {"image", user.image},
                    {"role", "Porteiro"},
                    {"datacreate", DateTimeOffset.Now.ToUnixTimeSeconds()}
                };
        }
        public BsonDocument RetornaUserMorador(UserMorador user)
        {
            if (user.image == null)
            {
                user.image = new byte[1];
            }
            return new BsonDocument{
                    {"_id", ObjectId.GenerateNewId()},
                    {"email", user.email.ToLower()},
                    {"password", passwordToHash(password: user.password)},
                    {"nome", user.nome.ToLower() },
                    {"bloco", user.bloco.ToLower() },
                    {"numeroapartamento", user.numeroapartamento.ToLower() },
                    {"nameCondominio", user.nameCondominio },
                    {"image", user.image},
                    {"role", "Morador"},
                    {"datacreate", DateTimeOffset.Now.ToUnixTimeSeconds()}

                };
        }
        public Boolean EmailExist(UserGenericLogin user, IMongoClient database)
        {

            try
            {
                if (user is UserAdm || user is UserPorteiro)
                {

                    if (database.GetDatabase("userscondominio").GetCollection<UserReferencia>("users").Find(UserReferencia => UserReferencia.email == user.email).ToList().Count >= 1)
                    {
                        return true;
                    }
                }
                else if (user is UserMorador)
                {
                    if (database.GetDatabase(RemoverCaracterEspecial(user.nameCondominio)).GetCollection<UserMorador>("usersMoradores").Find(UserMorador => UserMorador.email == user.email).ToList().Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (System.Exception)
            {

                return false;

            }





        }
        public string passwordToHash(string password)
        {
            using (SHA256 sHA256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
                byte[] passwordSha256Bytes = sHA256.ComputeHash(passwordBytes);
                StringBuilder sbSHA256 = new StringBuilder();
                for (int i = 0; i < passwordSha256Bytes.Length; i++)
                {
                    sbSHA256.Append(passwordSha256Bytes[i].ToString("X2"));
                }
                return sbSHA256.ToString();
            }

        }
        public string GenerateToken(UserGenericLogin user, double horas)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Settings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim("objectId", user.id.ToString()),
                    new Claim("nameCondominio", user.nameCondominio.ToString()),
                    new Claim(ClaimTypes.Role, user.role.ToString()),

                }),
                Expires = DateTime.UtcNow.AddHours(horas),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        public Boolean ValidateToken(HttpRequest request)
        {
            string jwtString = request.Headers["Authorization"].ToString().Split(" ")[1];
            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken validatedToken;
            TokenValidationParameters validationParameters = new TokenValidationParameters()
            {
                ValidateLifetime = true, // Because there is no expiration in the generated token
                ValidateAudience = false, // Because there is no audiance in the generated token
                ValidateIssuer = false,   // Because there is no issuer in the generated token
                ValidIssuer = "Sample",
                ValidAudience = "Sample",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Secret)), // The same key as the one that generate the token
                LifetimeValidator = (DateTime? notBefore, DateTime? expires, SecurityToken securityToken, TokenValidationParameters validationParameters) =>
                {
                    if (expires != null)
                    {
                        if (DateTime.Now < expires.Value.ToLocalTime()) return true;
                    }
                    return false;
                }
            };

            try
            {
                var p = tokenHandler.ValidateToken(jwtString, validationParameters, out validatedToken);
                return true;
            }
            catch (SecurityTokenException)
            {
                return false;
            }

        }
        public JObject UnGenereteToken(HttpRequest request)
        {
            string jwtString = request.Headers["Authorization"].ToString().Split(" ")[1];
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jsonToken = handler.ReadJwtToken(jwtString);
            JObject jsonClaim = JObject.Parse(jsonToken.ToString().Split("}.")[1]);
            return jsonClaim;
        }
        public string RemoverCaracterEspecial(string str)
        {
            /** Troca os caracteres acentuados por não acentuados **/
            string[] acentos = new string[] { "ç", "Ç", "á", "é", "í", "ó", "ú", "ý", "Á", "É", "Í", "Ó", "Ú", "Ý", "à", "è", "ì", "ò", "ù", "À", "È", "Ì", "Ò", "Ù", "ã", "õ", "ñ", "ä", "ë", "ï", "ö", "ü", "ÿ", "Ä", "Ë", "Ï", "Ö", "Ü", "Ã", "Õ", "Ñ", "â", "ê", "î", "ô", "û", "Â", "Ê", "Î", "Ô", "Û" };
            string[] semAcento = new string[] { "c", "C", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "Y", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U", "a", "o", "n", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "A", "O", "N", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U" };

            for (int i = 0; i < acentos.Length; i++)
            {
                str = str.Replace(acentos[i], semAcento[i]);
            }
            /** Troca os caracteres especiais da string por "" **/
            string[] caracteresEspeciais = { "¹", "²", "³", "£", "¢", "¬", "º", "¨", "\"", "'", ".", ",", "-", ":", "(", ")", "ª", "|", "\\\\", "°", "_", "@", "#", "!", "$", "%", "&", "*", ";", "/", "<", ">", "?", "[", "]", "{", "}", "=", "+", "§", "´", "`", "^", "~" };

            for (int i = 0; i < caracteresEspeciais.Length; i++)
            {
                str = str.Replace(caracteresEspeciais[i], "");
            }

            /** Troca os caracteres especiais da string por " " **/
            str = Regex.Replace(str, @"[^\w\.@-]", " ",
                                RegexOptions.None, TimeSpan.FromSeconds(1.5));

            return str.Trim().Replace(" ", "");
        }
        public string RemoverCaracterEspecialDeixarEspaco(string str)
        {
            /** Troca os caracteres acentuados por não acentuados **/
            string[] acentos = new string[] { "ç", "Ç", "á", "é", "í", "ó", "ú", "ý", "Á", "É", "Í", "Ó", "Ú", "Ý", "à", "è", "ì", "ò", "ù", "À", "È", "Ì", "Ò", "Ù", "ã", "õ", "ñ", "ä", "ë", "ï", "ö", "ü", "ÿ", "Ä", "Ë", "Ï", "Ö", "Ü", "Ã", "Õ", "Ñ", "â", "ê", "î", "ô", "û", "Â", "Ê", "Î", "Ô", "Û" };
            string[] semAcento = new string[] { "c", "C", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "Y", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U", "a", "o", "n", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "A", "O", "N", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U" };

            for (int i = 0; i < acentos.Length; i++)
            {
                str = str.Replace(acentos[i], semAcento[i]);
            }
            /** Troca os caracteres especiais da string por "" **/
            string[] caracteresEspeciais = { "¹", "²", "³", "£", "¢", "¬", "º", "¨", "\"", "'", ".", ",", "-", ":", "(", ")", "ª", "|", "\\\\", "°", "_", "@", "#", "!", "$", "%", "&", "*", ";", "/", "<", ">", "?", "[", "]", "{", "}", "=", "+", "§", "´", "`", "^", "~" };

            for (int i = 0; i < caracteresEspeciais.Length; i++)
            {
                str = str.Replace(caracteresEspeciais[i], "");
            }

            /** Troca os caracteres especiais da string por " " **/
            str = Regex.Replace(str, @"[^\w\.@-]", " ",
                                RegexOptions.None, TimeSpan.FromSeconds(1.5));

            return str;
        }
        public string RemoverBarraToken(string str)
        {
            return str.Replace("/","");
        }
        public string BloquarUser(HttpRequest request)
        {
            String ip = request.HttpContext.Connection.RemoteIpAddress.ToString();
            Console.Write(ip);

            return "";
        }
        public void SendEmail(Email email)
        {
            SmtpClient client = new SmtpClient("smtp.gmail.com", 587);
            client.Credentials = new NetworkCredential("seunegocioonlineagr@gmail.com", "35141543Rd");
            client.UseDefaultCredentials = false;
            client.EnableSsl = true;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;


            string to = email.email;
            string from = "noreply@noreply.com";
            MailMessage message = new MailMessage(from, to);
            message.IsBodyHtml = true;
            message.Subject = email.titulo;
            message.Body = email.body;
            try
            {
                client.Send(message);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n",
                    e.ToString());
            }
        }
        public void EmailConfimacao(UserGenericLogin user)
        {
            string url = URL + "api/confirmacaoEmail";
            Email email = new Email();
            email.email = user.email;
            email.titulo = "Redefinição de Senha";
            string image = "https://tse2.mm.bing.net/th?id=OIP.h3Eqt3wBHuM0tMbslVUcdwHaEo&pid=Api&P=0&w=300&h=300";
            string htmlimage = "<img src=" + image + " />";
            string html = string.Empty;
            string link = "<p><a href='" + url + "?token=" + GenerateToken(user, _timeExpiredTokenEMAIL) + "/'>Confirme Seu Email</a></p>";
            html = new StreamReader("C:\\Users\\Rodolfo\\git\\condominioApi\\Templates\\confirmed.html").ReadToEnd();
            html = html.Replace("<image/>", htmlimage);
            email.body = $"{html.Trim() + "\n" + link}";
            SendEmail(email);

        }
        public void EmailDeRedefinicaoDeSenha(UserGenericLogin user)
        {
            string url = URL + "api/editarsenha";
            Email email = new Email();
            email.email = user.email;
            email.titulo = "Redefinição de Senha";
            string image = "https://tse2.mm.bing.net/th?id=OIP.h3Eqt3wBHuM0tMbslVUcdwHaEo&pid=Api&P=0&w=300&h=300";
            string htmlimage = "<img src=" + image + " />";
            string html = string.Empty;
            string link = "<p><a href='" + url + "?token=" + GenerateToken(user, _timeExpiredTokenEMAIL) + "/'>Altere Sua Senha</a></p>";
            html = new StreamReader("C:\\Users\\Rodolfo\\git\\condominioApi\\Templates\\confirmed.html").ReadToEnd();
            html = html.Replace("<image/>", htmlimage);
            email.body = $"{html.Trim() + "\n" + link}";
            SendEmail(email);

        }



    }
}