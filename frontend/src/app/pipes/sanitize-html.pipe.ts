import { Pipe, PipeTransform } from "@angular/core";
import DOMPurify from "dompurify";

@Pipe({ name: "sanitizeHtml", standalone: true })
export class SanitizeHtmlPipe implements PipeTransform {
  private readonly sanitizer = DOMPurify(window);

  transform(value: string | null | undefined): string {
    if (!value) {
      return "";
    }
    return this.sanitizer.sanitize(value, {
      FORBID_TAGS: ["style"],
      FORBID_ATTR: ["style"],
    }) as string;
  }
}
