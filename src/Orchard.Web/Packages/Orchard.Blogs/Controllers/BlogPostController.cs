using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Orchard.Blogs.Extensions;
using Orchard.Blogs.Models;
using Orchard.Blogs.Services;
using Orchard.Blogs.ViewModels;
using Orchard.Data;
using Orchard.Localization;
using Orchard.ContentManagement;
using Orchard.Mvc.Results;
using Orchard.Security;
using Orchard.UI.Notify;

namespace Orchard.Blogs.Controllers {
    [ValidateInput(false)]
    public class BlogPostController : Controller, IUpdateModel {
        private readonly ISessionLocator _sessionLocator;
        private readonly IContentManager _contentManager;
        private readonly IAuthorizer _authorizer;
        private readonly INotifier _notifier;
        private readonly IBlogService _blogService;
        private readonly IBlogPostService _blogPostService;

        public BlogPostController(
            IOrchardServices services,
            ISessionLocator sessionLocator, IContentManager contentManager, 
            IAuthorizer authorizer,
            INotifier notifier, 
            IBlogService blogService,
            IBlogPostService blogPostService) {
            Services = services;
            _sessionLocator = sessionLocator;
            _contentManager = contentManager;
            _authorizer = authorizer;
            _notifier = notifier;
            _blogService = blogService;
            _blogPostService = blogPostService;
            T = NullLocalizer.Instance;
        }

        public IOrchardServices Services { get; set; }
        private Localizer T { get; set; }

        //TODO: (erikpo) Should think about moving the slug parameters and get calls and null checks up into a model binder or action filter
        public ActionResult Item(string blogSlug, string postSlug) {
            if (!_authorizer.Authorize(Permissions.ViewPost, T("Couldn't view blog post")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            BlogPost post = _blogPostService.Get(blog, postSlug);

            if (post == null)
                return new NotFoundResult();

            var model = new BlogPostViewModel {
                Blog = blog,
                BlogPost = _contentManager.BuildDisplayModel(post, "Detail")
            };

            return View(model);
        }

        public ActionResult ListByArchive(string blogSlug, string archiveData) {
            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            var archive = new ArchiveData(archiveData);
            var model = new BlogPostArchiveViewModel {
                Blog = blog,
                ArchiveData = archive,
                BlogPosts = _blogPostService.Get(blog, archive).Select(b => _contentManager.BuildDisplayModel(b, "Summary"))
            };

            return View(model);
        }

        public ActionResult Slugify(string value) {
            string slug = value;

            //TODO: (erikpo) Move this into a utility class
            if (!string.IsNullOrEmpty(value)) {
                Regex regex = new Regex("([^a-z0-9-_]?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                slug = value.Trim();
                slug = slug.Replace(' ', '-');
                slug = slug.Replace("---", "-");
                slug = slug.Replace("--", "-");
                slug = regex.Replace(slug, "");

                if (slug.Length * 2 < value.Length)
                    return Json("");

                if (slug.Length > 100)
                    slug = slug.Substring(0, 100);
            }

            return Json(slug);
        }

        public ActionResult Create(string blogSlug) {
            //TODO: (erikpo) Might think about moving this to an ActionFilter/Attribute
            if (!_authorizer.Authorize(Permissions.CreatePost, T("Not allowed to create blog post")))
                return new HttpUnauthorizedResult();
            
            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            var blogPost = _contentManager.BuildEditorModel(_contentManager.New<BlogPost>("blogpost"));
            blogPost.Item.Blog = blog;

            var model = new CreateBlogPostViewModel {
                BlogPost = blogPost
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult Create(string blogSlug, CreateBlogPostViewModel model) {
            if (!_authorizer.Authorize(Permissions.CreatePost, T("Couldn't create blog post")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            BlogPost blogPost = _contentManager.Create<BlogPost>("blogpost", bp => { bp.Blog = blog; });
            model.BlogPost = _contentManager.UpdateEditorModel(blogPost, this);

            if (!ModelState.IsValid)
                return View(model);

            //TEMP: (erikpo) ensure information has committed for this record
            var session = _sessionLocator.For(typeof(BlogPostRecord));
            session.Flush();

            return Redirect(Url.BlogPost(blogSlug, model.BlogPost.Item.Slug));
        }

        public ActionResult Edit(string blogSlug, string postSlug) {
            if (!_authorizer.Authorize(Permissions.ModifyPost, T("Couldn't edit blog post")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            BlogPost post = _blogPostService.Get(blog, postSlug);

            if (post == null)
                return new NotFoundResult();

            var model = new BlogPostEditViewModel {
                BlogPost = _contentManager.BuildEditorModel(post)
            };

            return View(model);
        }

        [HttpPost, ActionName("Edit")]
        public ActionResult EditPOST(string blogSlug, string postSlug) {
            if (!_authorizer.Authorize(Permissions.ModifyPost, T("Couldn't edit blog post")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            BlogPost post = _blogPostService.Get(blog, postSlug);

            if (post == null)
                return new NotFoundResult();

            var model = new BlogPostEditViewModel {
                BlogPost = _contentManager.UpdateEditorModel(post, this)
            };

            TryUpdateModel(model);

            if (ModelState.IsValid==false) {
                Services.TransactionManager.Cancel();
                return View(model);
            }

            _notifier.Information(T("Blog post information updated."));
            return Redirect(Url.BlogPostEdit(blog.Slug, post.Slug));
        }

        [HttpPost]
        public ActionResult Delete(string blogSlug, string postSlug) {
            if (!_authorizer.Authorize(Permissions.DeletePost, T("Couldn't delete blog post")))
                return new HttpUnauthorizedResult();

            //TODO: (erikpo) Move looking up the current blog up into a modelbinder
            Blog blog = _blogService.Get(blogSlug);

            if (blog == null)
                return new NotFoundResult();

            BlogPost post = _blogPostService.Get(blog, postSlug);

            if (post == null)
                return new NotFoundResult();

            _blogPostService.Delete(post);

            _notifier.Information(T("Blog post was successfully deleted"));

            return Redirect(Url.BlogForAdmin(blogSlug));
        }

        bool IUpdateModel.TryUpdateModel<TModel>(TModel model, string prefix, string[] includeProperties, string[] excludeProperties) {
            return TryUpdateModel(model, prefix, includeProperties, excludeProperties);
        }
        void IUpdateModel.AddModelError(string key, LocalizedString errorMessage) {
            ModelState.AddModelError(key, errorMessage.ToString());
        }
    }
}